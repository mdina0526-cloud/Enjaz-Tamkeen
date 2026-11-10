using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Infrastructure;

namespace SalesSystem.Api.Services;

public class RentalService(AppDbContext db, AuditService audit, BusinessClock clock, ICurrentUser currentUser, FileStorage storage, DailyCloseService dailyClose)
{
    private record RentalRow(Rental Rental, decimal Paid, int PaymentsCount);

    public async Task<IReadOnlyList<RentalListItemDto>> ListAsync(RentalQuery query, CancellationToken ct)
    {
        var rows = await QueryRows(query).ToListAsync(ct);
        var today = clock.Today;
        var list = rows.Select(r => ToListItem(r, today));
        if (query.Status.HasValue) list = list.Where(r => r.Status == query.Status);
        return list.OrderBy(r => r.Status == RentalStatus.Paid).ThenBy(r => r.DueDate).ToList();
    }

    public async Task<RentalSummaryDto> SummaryAsync(RentalQuery query, CancellationToken ct)
    {
        var items = await ListAsync(query with { Status = null }, ct);
        int Count(RentalStatus s) => items.Count(i => i.Status == s);
        return new RentalSummaryDto(
            items.Count, Count(RentalStatus.Upcoming), Count(RentalStatus.Due), Count(RentalStatus.PartiallyPaid), Count(RentalStatus.Paid),
            Count(RentalStatus.Overdue), items.Sum(i => i.Amount), items.Sum(i => i.PaidAmount), items.Sum(i => i.RemainingAmount),
            items.Where(i => i.Status == RentalStatus.Overdue).Sum(i => i.RemainingAmount));
    }

    private IQueryable<RentalRow> QueryRows(RentalQuery query)
    {
        var q = db.Rentals.AsNoTracking().AsQueryable();
        if (!query.IncludeArchived) q = q.Where(r => !r.IsArchived);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(r => r.Title.Contains(s) || r.BeneficiaryName.Contains(s) || (r.Location != null && r.Location.Contains(s)));
        }
        if (query.From.HasValue) q = q.Where(r => r.DueDate >= query.From.Value);
        if (query.To.HasValue) q = q.Where(r => r.DueDate <= query.To.Value);

        return q.Select(r => new RentalRow(
            r,
            r.Payments.Where(p => !p.IsVoided).Sum(p => (decimal?)p.Amount) ?? 0,
            r.Payments.Count(p => !p.IsVoided)));
    }

    public async Task<RentalDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var r = await db.Rentals.AsNoTracking().Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("الإيجار غير موجود");
        var paid = r.Payments.Where(p => !p.IsVoided).Sum(p => p.Amount);
        return new RentalDetailDto(
            r.Id, r.Title, r.PropertyType, r.Location, r.BeneficiaryName, r.BeneficiaryPhone, r.Amount, paid, Math.Max(0, r.Amount - paid),
            r.DueDate, RentalStatusCalculator.Compute(r.Amount, paid, r.DueDate, clock.Today), r.Notes, r.IsArchived, r.CreatedAtUtc,
            r.Payments.OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.Id).Select(p => new RentalPaymentDto(
                p.Id, p.Amount, p.Method, p.PaidOn, p.Reference, p.Notes, p.AttachmentStoredName != null, p.AttachmentOriginalName,
                p.IsVoided, p.VoidReason, p.CreatedAtUtc)).ToList());
    }

    public async Task<RentalDetailDto> CreateAsync(RentalRequest req, CancellationToken ct)
    {
        var rental = new Rental
        {
            Title = req.Title.Trim(),
            PropertyType = req.PropertyType,
            Location = req.Location?.Trim(),
            BeneficiaryName = req.BeneficiaryName.Trim(),
            BeneficiaryPhone = req.BeneficiaryPhone?.Trim(),
            Amount = Math.Round(req.Amount, 2, MidpointRounding.AwayFromZero),
            DueDate = req.DueDate,
            Notes = req.Notes?.Trim(),
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = currentUser.UserId,
        };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Rentals.Add(rental);
        await db.SaveChangesAsync(ct);
        audit.Add(AuditActions.Create, "Rental", rental.Id, new { rental.Title, rental.BeneficiaryName, rental.Amount, rental.DueDate });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(rental.Id, ct);
    }

    public async Task<RentalDetailDto> UpdateAsync(int id, RentalRequest req, CancellationToken ct)
    {
        var r = await db.Rentals.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("الإيجار غير موجود");
        var paid = r.Payments.Where(p => !p.IsVoided).Sum(p => p.Amount);
        var amount = Math.Round(req.Amount, 2, MidpointRounding.AwayFromZero);
        if (amount < paid) throw AppException.BadRequest("لا يمكن أن تكون قيمة الإيجار أقل من المبلغ المدفوع");

        var before = new { r.Title, r.BeneficiaryName, r.Amount, r.DueDate, r.Location };
        r.Title = req.Title.Trim();
        r.PropertyType = req.PropertyType;
        r.Location = req.Location?.Trim();
        r.BeneficiaryName = req.BeneficiaryName.Trim();
        r.BeneficiaryPhone = req.BeneficiaryPhone?.Trim();
        r.Amount = amount;
        r.DueDate = req.DueDate;
        r.Notes = req.Notes?.Trim();
        audit.Add(AuditActions.Update, "Rental", r.Id, new { before, after = new { r.Title, r.BeneficiaryName, r.Amount, r.DueDate, r.Location } });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task SetArchivedAsync(int id, bool archived, CancellationToken ct)
    {
        var r = await db.Rentals.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("الإيجار غير موجود");
        r.IsArchived = archived;
        audit.Add(archived ? AuditActions.Archive : AuditActions.Update, "Rental", r.Id, new { archived });
        await db.SaveChangesAsync(ct);
    }

    public async Task<(RentalDetailDto Rental, bool Replayed)> AddPaymentAsync(int rentalId, RentalPaymentRequest req, string? idempotencyKey, CancellationToken ct)
    {
        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (idempotencyKey is { Length: > 64 }) throw AppException.BadRequest("مفتاح عدم التكرار طويل جداً");
        if (idempotencyKey is not null && await db.RentalPayments.AnyAsync(p => p.IdempotencyKey == idempotencyKey, ct))
            return (await GetAsync(rentalId, ct), true);

        if (req.PaidOn > clock.Today) throw AppException.BadRequest("لا يمكن أن يكون تاريخ السداد في المستقبل");
        await dailyClose.EnsureOpenAsync(req.PaidOn, ct);
        if (!Enum.IsDefined(req.Method)) throw AppException.BadRequest("طريقة الدفع غير صالحة");

        (string StoredName, string ContentType)? file = req.Attachment is null ? null : await storage.SaveAsync(req.Attachment, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Row lock: a concurrent payment on the same rental waits here, then sees the updated paid total.
            if (await db.Rentals.Where(x => x.Id == rentalId).ExecuteUpdateAsync(s => s.SetProperty(x => x.LockVersion, x => x.LockVersion + 1), ct) == 0)
                throw AppException.NotFound("الإيجار غير موجود");
            var rental = await db.Rentals.FirstAsync(x => x.Id == rentalId, ct);
            if (rental.IsArchived) throw AppException.BadRequest("لا يمكن تسجيل دفعة على إيجار مؤرشف");

            var paid = await db.RentalPayments.Where(p => p.RentalId == rentalId && !p.IsVoided).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
            var amount = Math.Round(req.Amount, 2, MidpointRounding.AwayFromZero);
            var remaining = rental.Amount - paid;
            if (remaining <= 0) throw AppException.Conflict("تم سداد هذا الإيجار بالكامل");
            if (amount > remaining) throw AppException.BadRequest($"المبلغ يتجاوز المتبقي ({remaining:N2})");

            var payment = new RentalPayment
            {
                RentalId = rentalId,
                Amount = amount,
                Method = req.Method,
                PaidOn = req.PaidOn,
                Reference = req.Reference?.Trim(),
                Notes = req.Notes?.Trim(),
                AttachmentStoredName = file?.StoredName,
                AttachmentOriginalName = file is null ? null : Path.GetFileName(req.Attachment!.FileName),
                AttachmentContentType = file?.ContentType,
                IdempotencyKey = idempotencyKey,
                CreatedAtUtc = clock.UtcNow,
                CreatedByUserId = currentUser.UserId,
            };
            db.RentalPayments.Add(payment);
            await db.SaveChangesAsync(ct);
            audit.Add(AuditActions.Create, "RentalPayment", payment.Id, new { rentalId, amount, Method = req.Method.ToString(), req.PaidOn, HasAttachment = file is not null });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException) when (idempotencyKey is not null)
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            if (await db.RentalPayments.AnyAsync(p => p.IdempotencyKey == idempotencyKey, ct)) return (await GetAsync(rentalId, ct), true);
            throw;
        }

        return (await GetAsync(rentalId, ct), false);
    }

    public async Task<RentalDetailDto> VoidPaymentAsync(int rentalId, int paymentId, string reason, CancellationToken ct)
    {
        var p = await db.RentalPayments.FirstOrDefaultAsync(x => x.Id == paymentId && x.RentalId == rentalId, ct)
                ?? throw AppException.NotFound("الدفعة غير موجودة");
        if (p.IsVoided) throw AppException.Conflict("الدفعة ملغاة مسبقاً");
        await dailyClose.EnsureOpenAsync(p.PaidOn, ct);
        p.IsVoided = true;
        p.VoidReason = reason.Trim();
        p.VoidedAtUtc = clock.UtcNow;
        audit.Add(AuditActions.Void, "RentalPayment", p.Id, new { rentalId, p.Amount, Reason = p.VoidReason });
        await db.SaveChangesAsync(ct);
        return await GetAsync(rentalId, ct);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetAttachmentAsync(int rentalId, int paymentId, CancellationToken ct)
    {
        var p = await db.RentalPayments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == paymentId && x.RentalId == rentalId, ct)
                ?? throw AppException.NotFound("الدفعة غير موجودة");
        if (p.AttachmentStoredName is null) throw AppException.NotFound("لا يوجد مرفق لهذه الدفعة");
        var stream = storage.OpenRead(p.AttachmentStoredName) ?? throw AppException.NotFound("ملف المرفق غير موجود");
        return (stream, p.AttachmentContentType ?? "application/octet-stream", p.AttachmentOriginalName ?? p.AttachmentStoredName);
    }

    private static RentalListItemDto ToListItem(RentalRow row, DateOnly today)
    {
        var r = row.Rental;
        return new RentalListItemDto(r.Id, r.Title, r.PropertyType, r.Location, r.BeneficiaryName, r.Amount, row.Paid,
            Math.Max(0, r.Amount - row.Paid), r.DueDate, RentalStatusCalculator.Compute(r.Amount, row.Paid, r.DueDate, today), r.IsArchived, row.PaymentsCount);
    }
}
