using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Infrastructure;

namespace SalesSystem.Api.Services;

public class OrderService(
    AppDbContext db, ICurrentUser currentUser, AuditService audit, SettingsService settings, EmployeeService employees, BusinessClock clock,
    ILogger<OrderService> logger)
{
    /// <summary>How far back an employee may date an operation. Admins may record any past date.</summary>
    public const int EmployeeBackdateDays = 7;

    /// <summary>
    /// Base query restricted to what the caller may see. Employees only ever get their own orders,
    /// whatever ids or filters they send.
    /// </summary>
    private IQueryable<SalesOrder> Scoped()
    {
        var q = db.SalesOrders.AsNoTracking();
        if (currentUser.IsAdmin) return q;
        var employeeId = currentUser.EmployeeId ?? throw AppException.Forbidden();
        return q.Where(o => o.EmployeeId == employeeId);
    }

    public async Task<PagedResult<OrderListItemDto>> ListAsync(OrderQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);
        // The employee filter is meaningless for employees: their scope is already fixed to themselves.
        var q = ApplyFilters(Scoped(), currentUser.IsAdmin ? query : query with { EmployeeId = null });

        var total = await q.CountAsync(ct);
        var items = await Sort(q, query.SortBy, query.SortDir)
            .Skip((page - 1) * size).Take(size)
            .Select(ToListItem)
            .ToListAsync(ct);
        return new PagedResult<OrderListItemDto>(items, total, page, size);
    }

    public IQueryable<SalesOrder> ApplyFilters(IQueryable<SalesOrder> q, OrderQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(o => o.OrderNumber.Contains(s) || (o.Notes != null && o.Notes.Contains(s))
                             || o.Items.Any(i => i.ServiceName.Contains(s) || (i.Description != null && i.Description.Contains(s)))
                             || o.Employee.User.FullName.Contains(s));
        }
        if (query.From.HasValue) q = q.Where(o => o.OperationDate >= query.From.Value);
        if (query.To.HasValue) q = q.Where(o => o.OperationDate <= query.To.Value);
        if (query.EmployeeId.HasValue) q = q.Where(o => o.EmployeeId == query.EmployeeId);
        if (query.PaymentMethod.HasValue) q = q.Where(o => o.PaymentMethod == query.PaymentMethod);
        if (query.Status.HasValue) q = q.Where(o => o.Status == query.Status);
        if (query.OrderTypeId.HasValue) q = q.Where(o => o.OrderTypeId == query.OrderTypeId);
        if (query.ServiceTypeId.HasValue) q = q.Where(o => o.Items.Any(i => i.ServiceTypeId == query.ServiceTypeId));
        return q;
    }

    private static IQueryable<SalesOrder> Sort(IQueryable<SalesOrder> q, string? sortBy, string? sortDir)
    {
        var asc = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<SalesOrder> ordered = sortBy?.ToLowerInvariant() switch
        {
            "number" => asc ? q.OrderBy(o => o.OrderNumber) : q.OrderByDescending(o => o.OrderNumber),
            "amount" => asc ? q.OrderBy(o => o.Subtotal) : q.OrderByDescending(o => o.Subtotal),
            "deduction" => asc ? q.OrderBy(o => o.DeductionAmount) : q.OrderByDescending(o => o.DeductionAmount),
            "net" => asc ? q.OrderBy(o => o.NetAmount) : q.OrderByDescending(o => o.NetAmount),
            "employee" => asc ? q.OrderBy(o => o.Employee.User.FullName) : q.OrderByDescending(o => o.Employee.User.FullName),
            "status" => asc ? q.OrderBy(o => o.Status) : q.OrderByDescending(o => o.Status),
            _ => asc ? q.OrderBy(o => o.OperationDate).ThenBy(o => o.CreatedAtUtc) : q.OrderByDescending(o => o.OperationDate).ThenByDescending(o => o.CreatedAtUtc),
        };
        return asc ? ordered.ThenBy(o => o.Id) : ordered.ThenByDescending(o => o.Id);
    }

    public async Task<OrderDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var o = await Scoped()
            .Include(x => x.Items).Include(x => x.Payments).Include(x => x.DeductionRecord).Include(x => x.OrderType)
            .Include(x => x.Employee).ThenInclude(e => e.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        // 404 (not 403) so employees cannot probe which order ids exist.
        return o is null ? throw AppException.NotFound("الطلب غير موجود") : ToDetail(o);
    }

    public async Task<DeductionInfoDto> DeductionInfoAsync(int? employeeId, CancellationToken ct)
    {
        var id = ResolveEmployeeId(employeeId);
        var name = await db.Employees.Where(e => e.Id == id).Select(e => e.User.FullName).FirstOrDefaultAsync(ct)
                   ?? throw AppException.NotFound("الموظف غير موجود");
        var s = await settings.GetDeductionSettingsAsync(ct);
        return new DeductionInfoDto(id, name, await employees.CurrentPercentageAsync(id, ct), s.Enabled, s.Treatment, s.Label);
    }

    public async Task<(OrderDetailDto Order, bool Replayed)> CreateAsync(CreateOrderRequest req, string? idempotencyKey, CancellationToken ct)
    {
        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (idempotencyKey is { Length: > 64 }) throw AppException.BadRequest("مفتاح عدم التكرار طويل جداً");

        if (idempotencyKey is not null && await FindByIdempotencyKeyAsync(idempotencyKey, ct) is { } existing)
            return (existing, true);

        var employeeId = ResolveEmployeeId(req.EmployeeId);
        var employee = await db.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == employeeId, ct)
                       ?? throw AppException.NotFound("الموظف غير موجود");
        if (!employee.User.IsActive) throw AppException.BadRequest("لا يمكن تسجيل طلب لموظف معطل");
        if (req.Items.Count == 0) throw AppException.BadRequest("يجب إضافة خدمة واحدة على الأقل");
        if (!await db.PaymentMethods.AnyAsync(m => m.Id == req.PaymentMethod && m.IsActive, ct))
            throw AppException.BadRequest("طريقة الدفع غير صالحة");

        var orderType = await db.OrderTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.OrderTypeId, ct);
        if (orderType is not { IsActive: true }) throw AppException.BadRequest("نوع الطلب غير صالح أو غير مفعّل");

        var operationDate = ValidateOperationDate(req.OperationDate);

        var serviceIds = req.Items.Select(i => i.ServiceTypeId).Distinct().ToList();
        var services = await db.Services.AsNoTracking().Where(sv => serviceIds.Contains(sv.Id)).ToDictionaryAsync(sv => sv.Id, ct);
        foreach (var id in serviceIds)
        {
            if (!services.TryGetValue(id, out var sv) || !sv.IsActive) throw AppException.BadRequest("نوع الخدمة غير صالح أو غير مفعّل");
            if (sv.OrderTypeId.HasValue && sv.OrderTypeId != orderType.Id)
                throw AppException.BadRequest($"الخدمة \"{sv.Name}\" لا تتبع نوع الطلب \"{orderType.Name}\"");
        }

        var items = req.Items.Select(i => new SalesOrderItem
        {
            ServiceTypeId = i.ServiceTypeId,
            ServiceName = services[i.ServiceTypeId].Name,
            Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim(),
            Quantity = i.Quantity,
            UnitPrice = Math.Round(i.UnitPrice, 2, MidpointRounding.AwayFromZero),
            LineTotal = DeductionCalculator.LineTotal(i.Quantity, i.UnitPrice),
        }).ToList();
        if (items.Any(i => i.UnitPrice <= 0 || i.Quantity <= 0))
            throw AppException.BadRequest("بيانات الخدمات غير صالحة");

        var subtotal = items.Sum(i => i.LineTotal);
        var deductionSettings = await settings.GetDeductionSettingsAsync(ct);
        var rule = await db.EmployeeDeductionRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.EffectiveToUtc == null, ct);
        var calc = DeductionCalculator.Calculate(subtotal, rule?.Percentage ?? 0, deductionSettings.Treatment, deductionSettings.Enabled);

        var now = clock.UtcNow;
        var order = new SalesOrder
        {
            OrderNumber = "TMP-" + Guid.NewGuid().ToString("N"),
            EmployeeId = employeeId,
            OrderTypeId = orderType.Id,
            OperationDate = operationDate,
            // Orders recorded by the admin are final; employee orders await review.
            Status = currentUser.IsAdmin ? OrderStatus.Completed : OrderStatus.Pending,
            PaymentMethod = req.PaymentMethod,
            Subtotal = calc.Gross,
            DeductionPercentage = calc.Percentage,
            DeductionAmount = calc.DeductionAmount,
            NetAmount = calc.NetAmount,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = now,
            CreatedByUserId = currentUser.UserId,
            CompletedAtUtc = currentUser.IsAdmin ? now : null,
            CompletedByUserId = currentUser.IsAdmin ? currentUser.UserId : null,
            Items = items,
        };
        order.Payments.Add(new Payment { Amount = calc.Gross, Method = req.PaymentMethod, PaidAtUtc = now });
        order.DeductionRecord = new DeductionRecord
        {
            EmployeeId = employeeId,
            DeductionRuleId = rule?.Id,
            Percentage = calc.Percentage,
            BaseAmount = calc.Gross,
            DeductionAmount = calc.DeductionAmount,
            NetAmount = calc.NetAmount,
            Treatment = deductionSettings.Treatment,
            CreatedAtUtc = now,
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.SalesOrders.Add(order);
            await db.SaveChangesAsync(ct);

            order.OrderNumber = $"SO-{clock.ToLocal(now):yyyyMMdd}-{order.Id:D6}";
            audit.Add(AuditActions.Create, "SalesOrder", order.Id, new
            {
                order.OrderNumber, order.EmployeeId, order.Subtotal, order.DeductionPercentage, order.DeductionAmount, order.NetAmount,
                PaymentMethod = order.PaymentMethod.ToString(), Items = items.Count,
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            logger.LogInformation(
                "Order {OrderNumber} created by user {UserId} for employee {EmployeeId}: gross {Gross}, deduction {Deduction} ({Percentage}%), net {Net}, method {PaymentMethod}",
                order.OrderNumber, currentUser.UserId, employeeId, order.Subtotal, order.DeductionAmount, order.DeductionPercentage, order.NetAmount, order.PaymentMethod);
        }
        catch (DbUpdateException) when (idempotencyKey is not null)
        {
            // A concurrent duplicate with the same key won the race: return that one instead.
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            if (await FindByIdempotencyKeyAsync(idempotencyKey, ct) is { } dup)
            {
                logger.LogInformation("Duplicate submission collapsed into order {OrderId}", dup.Id);
                return (dup, true);
            }
            throw;
        }

        return (await GetAsync(order.Id, ct), false);
    }

    public async Task<OrderDetailDto> ApproveAsync(int id, CancellationToken ct)
    {
        var order = await db.SalesOrders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw AppException.NotFound("الطلب غير موجود");
        if (order.Status != OrderStatus.Pending) throw AppException.Conflict("لا يمكن اعتماد هذا الطلب في حالته الحالية");

        order.Status = OrderStatus.Completed;
        order.CompletedAtUtc = clock.UtcNow;
        order.CompletedByUserId = currentUser.UserId;
        audit.Add(AuditActions.Approve, "SalesOrder", order.Id, new { order.OrderNumber });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Cancels without deleting: the order, payment and deduction stay on record, flagged as cancelled/voided.</summary>
    public async Task<OrderDetailDto> CancelAsync(int id, string reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await db.SalesOrders.Include(o => o.Payments).Include(o => o.DeductionRecord)
            .FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw AppException.NotFound("الطلب غير موجود");
        if (order.Status == OrderStatus.Cancelled) throw AppException.Conflict("الطلب ملغي مسبقاً");

        var now = clock.UtcNow;
        var previous = order.Status;
        order.Status = OrderStatus.Cancelled;
        order.CancelledAtUtc = now;
        order.CancelledByUserId = currentUser.UserId;
        order.CancellationReason = reason.Trim();
        foreach (var p in order.Payments.Where(p => p.Status == PaymentStatus.Completed))
        {
            p.Status = PaymentStatus.Voided;
            p.VoidedAtUtc = now;
        }
        if (order.DeductionRecord is not null) order.DeductionRecord.IsVoided = true;

        audit.Add(AuditActions.Cancel, "SalesOrder", order.Id, new { order.OrderNumber, PreviousStatus = previous.ToString(), Reason = order.CancellationReason, order.Subtotal });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogWarning("Order {OrderNumber} cancelled by user {UserId} (was {PreviousStatus}): {Reason}",
            order.OrderNumber, currentUser.UserId, previous, order.CancellationReason);
        return await GetAsync(id, ct);
    }

    private DateOnly ValidateOperationDate(DateOnly? requested)
    {
        var today = clock.Today;
        var date = requested ?? today;
        if (date > today) throw AppException.BadRequest("لا يمكن أن يكون تاريخ العملية في المستقبل");
        if (date.Year < 2000) throw AppException.BadRequest("تاريخ العملية غير صالح");
        if (!currentUser.IsAdmin && date < today.AddDays(-EmployeeBackdateDays))
            throw AppException.BadRequest($"لا يمكن تسجيل عملية بتاريخ أقدم من {EmployeeBackdateDays} أيام");
        return date;
    }

    private int ResolveEmployeeId(int? requested)
    {
        if (currentUser.IsAdmin)
            return requested ?? throw AppException.BadRequest("يجب تحديد الموظف المسؤول عن الطلب");

        var own = currentUser.EmployeeId ?? throw AppException.Forbidden();
        if (requested.HasValue && requested.Value != own)
            throw AppException.Forbidden("لا يمكنك تسجيل أو عرض عمليات باسم موظف آخر");
        return own;
    }

    private async Task<OrderDetailDto?> FindByIdempotencyKeyAsync(string key, CancellationToken ct)
    {
        var match = await db.SalesOrders.AsNoTracking().Where(o => o.IdempotencyKey == key)
            .Select(o => new { o.Id, o.CreatedByUserId }).FirstOrDefaultAsync(ct);
        if (match is null) return null;
        if (match.CreatedByUserId != currentUser.UserId) throw AppException.Conflict("مفتاح عدم التكرار مستخدم لطلب آخر");
        return await GetAsync(match.Id, ct);
    }

    public static readonly System.Linq.Expressions.Expression<Func<SalesOrder, OrderListItemDto>> ToListItem = o => new OrderListItemDto(
        o.Id, o.OrderNumber, o.OperationDate, o.CreatedAtUtc, o.EmployeeId, o.Employee.User.FullName, o.OrderTypeId, o.OrderType.Name, o.Status,
        o.PaymentMethod, o.Items.OrderBy(i => i.Id).Select(i => i.ServiceName).FirstOrDefault(), o.Items.Count, o.Subtotal, o.DeductionPercentage,
        o.DeductionAmount, o.NetAmount);

    private static OrderDetailDto ToDetail(SalesOrder o) => new(
        o.Id, o.OrderNumber, o.OperationDate, o.CreatedAtUtc, o.EmployeeId, o.Employee.User.FullName, o.Employee.EmployeeCode, o.OrderTypeId,
        o.OrderType.Name, o.Status,
        o.PaymentMethod, o.Subtotal, o.DeductionPercentage, o.DeductionAmount, o.NetAmount, o.DeductionRecord?.Treatment, o.Notes,
        o.CompletedAtUtc, o.CancelledAtUtc, o.CancellationReason,
        o.Items.OrderBy(i => i.Id).Select(i => new OrderItemDto(i.Id, i.ServiceTypeId, i.ServiceName, i.Description, i.Quantity, i.UnitPrice, i.LineTotal)).ToList(),
        o.Payments.OrderBy(p => p.Id).Select(p => new PaymentDto(p.Id, p.Amount, p.Method, p.Status, p.PaidAtUtc, p.VoidedAtUtc)).ToList());
}
