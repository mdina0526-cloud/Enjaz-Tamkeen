using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Infrastructure;

namespace SalesSystem.Api.Services;

public class EmployeeService(AppDbContext db, PasswordService passwords, AuditService audit, BusinessClock clock, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<EmployeeListItemDto>> ListAsync(string? search, bool? isActive, CancellationToken ct)
    {
        var q = db.Employees.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(e => e.User.FullName.Contains(s) || e.User.Username.Contains(s) || e.EmployeeCode.Contains(s) || (e.Phone != null && e.Phone.Contains(s)));
        }
        if (isActive.HasValue) q = q.Where(e => e.User.IsActive == isActive.Value);

        return await q.OrderBy(e => e.EmployeeCode).Select(e => new EmployeeListItemDto(
                e.Id, e.UserId, e.EmployeeCode, e.User.FullName, e.User.Username, e.User.Email, e.Phone, e.JobTitle, e.User.IsActive,
                e.DeductionRules.Where(r => r.EffectiveToUtc == null).Select(r => r.Percentage).FirstOrDefault(),
                e.SalesOrders.Count(o => o.Status != OrderStatus.Cancelled),
                e.SalesOrders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => (decimal?)o.Subtotal) ?? 0,
                e.User.LastLoginAtUtc, e.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EmployeeLookupDto>> LookupAsync(CancellationToken ct) =>
        await db.Employees.AsNoTracking().OrderBy(e => e.User.FullName)
            .Select(e => new EmployeeLookupDto(e.Id, e.User.FullName, e.EmployeeCode, e.User.IsActive))
            .ToListAsync(ct);

    public async Task<EmployeeDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var e = await db.Employees.AsNoTracking().Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("الموظف غير موجود");
        return ToDetail(e, await CurrentPercentageAsync(id, ct));
    }

    public async Task<EmployeeDetailDto> CreateAsync(CreateEmployeeRequest req, CancellationToken ct)
    {
        PasswordService.EnsureStrong(req.Password);
        // Stored lower-case so sign-in is case-insensitive on every database.
        var username = req.Username.Trim().ToLowerInvariant();
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLowerInvariant();
        await EnsureUniqueAsync(username, email, null, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = new User
        {
            Username = username,
            FullName = req.FullName.Trim(),
            Email = email,
            RoleId = await db.Roles.Where(r => r.Name == RoleNames.Employee).Select(r => r.Id).FirstAsync(ct),
            CreatedAtUtc = clock.UtcNow,
        };
        user.PasswordHash = passwords.Hash(user, req.Password);

        var employee = new Employee
        {
            User = user,
            EmployeeCode = await NextEmployeeCodeAsync(ct),
            LoginSlug = LoginLinks.NewSlug(),
            Phone = req.Phone?.Trim(),
            JobTitle = req.JobTitle?.Trim(),
            HireDate = req.HireDate,
            CreatedAtUtc = clock.UtcNow,
        };
        employee.DeductionRules.Add(new EmployeeDeductionRule
        {
            Percentage = req.DeductionPercentage,
            EffectiveFromUtc = clock.UtcNow,
            CreatedByUserId = currentUser.UserId,
            Notes = "النسبة الابتدائية",
        });
        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);

        audit.Add(AuditActions.Create, "Employee", employee.Id, new { employee.EmployeeCode, user.Username, user.FullName, req.DeductionPercentage });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDetail(employee, req.DeductionPercentage);
    }

    public async Task<EmployeeDetailDto> UpdateAsync(int id, UpdateEmployeeRequest req, CancellationToken ct)
    {
        var e = await LoadAsync(id, ct);
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLowerInvariant();
        await EnsureUniqueAsync(null, email, e.UserId, ct);

        var before = new { e.User.FullName, e.User.Email, e.Phone, e.JobTitle, e.HireDate };
        e.User.FullName = req.FullName.Trim();
        e.User.Email = email;
        e.Phone = req.Phone?.Trim();
        e.JobTitle = req.JobTitle?.Trim();
        e.HireDate = req.HireDate;
        audit.Add(AuditActions.Update, "Employee", e.Id, new { before, after = new { e.User.FullName, e.User.Email, e.Phone, e.JobTitle, e.HireDate } });
        await db.SaveChangesAsync(ct);
        return ToDetail(e, await CurrentPercentageAsync(id, ct));
    }

    public async Task SetStatusAsync(int id, bool isActive, CancellationToken ct)
    {
        var e = await LoadAsync(id, ct);
        if (e.User.IsActive == isActive) return;
        e.User.IsActive = isActive;
        // Invalidate existing tokens when deactivating.
        e.User.SecurityStamp = Guid.NewGuid().ToString("N");
        audit.Add(isActive ? AuditActions.Activate : AuditActions.Deactivate, "Employee", e.Id);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Issues a new personal login link; the previous one stops working immediately.</summary>
    public async Task<EmployeeDetailDto> RegenerateLoginLinkAsync(int id, CancellationToken ct)
    {
        var e = await LoadAsync(id, ct);
        e.LoginSlug = LoginLinks.NewSlug();
        audit.Add(AuditActions.RegenerateLoginLink, "Employee", e.Id);
        await db.SaveChangesAsync(ct);
        return ToDetail(e, await CurrentPercentageAsync(id, ct));
    }

    public async Task ResetPasswordAsync(int id, string newPassword, CancellationToken ct)
    {
        PasswordService.EnsureStrong(newPassword);
        var e = await LoadAsync(id, ct);
        e.User.PasswordHash = passwords.Hash(e.User, newPassword);
        e.User.SecurityStamp = Guid.NewGuid().ToString("N");
        e.User.FailedLoginCount = 0;
        e.User.LockoutEndUtc = null;
        audit.Add(AuditActions.ResetPassword, "Employee", e.Id);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Closes the current rule and opens a new one. Past orders keep their own snapshot.</summary>
    public async Task SetDeductionAsync(int id, SetDeductionRequest req, CancellationToken ct)
    {
        await LoadAsync(id, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = clock.UtcNow;
        var current = await db.EmployeeDeductionRules.Where(r => r.EmployeeId == id && r.EffectiveToUtc == null).ToListAsync(ct);
        var oldPct = current.Select(r => (decimal?)r.Percentage).FirstOrDefault();
        foreach (var r in current) r.EffectiveToUtc = now;

        db.EmployeeDeductionRules.Add(new EmployeeDeductionRule
        {
            EmployeeId = id,
            Percentage = req.Percentage,
            EffectiveFromUtc = now,
            CreatedByUserId = currentUser.UserId,
            Notes = req.Notes?.Trim(),
        });
        audit.Add(AuditActions.ChangeDeduction, "Employee", id, new { from = oldPct, to = req.Percentage, req.Notes });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<DeductionRuleDto>> DeductionHistoryAsync(int id, CancellationToken ct)
    {
        await LoadAsync(id, ct);
        return await db.EmployeeDeductionRules.AsNoTracking()
            .Where(r => r.EmployeeId == id)
            .OrderByDescending(r => r.EffectiveFromUtc).ThenByDescending(r => r.Id)
            .Select(r => new DeductionRuleDto(r.Id, r.Percentage, r.EffectiveFromUtc, r.EffectiveToUtc, r.Notes,
                db.Users.Where(u => u.Id == r.CreatedByUserId).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<EmployeeStatsDto> StatsAsync(int id, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        await LoadAsync(id, ct);
        var q = db.SalesOrders.AsNoTracking().Where(o => o.EmployeeId == id);
        if (from.HasValue) q = q.Where(o => o.OperationDate >= from.Value);
        if (to.HasValue) q = q.Where(o => o.OperationDate <= to.Value);
        var active = q.Where(o => o.Status != OrderStatus.Cancelled);

        return new EmployeeStatsDto(
            await active.CountAsync(ct),
            await active.SumAsync(o => (decimal?)o.Subtotal, ct) ?? 0,
            await active.SumAsync(o => (decimal?)o.DeductionAmount, ct) ?? 0,
            await active.SumAsync(o => (decimal?)o.NetAmount, ct) ?? 0,
            await active.Where(o => o.PaymentMethod == PaymentMethod.Cash).SumAsync(o => (decimal?)o.Subtotal, ct) ?? 0,
            await active.Where(o => o.PaymentMethod == PaymentMethod.Card).SumAsync(o => (decimal?)o.Subtotal, ct) ?? 0,
            await q.CountAsync(o => o.Status == OrderStatus.Cancelled, ct));
    }

    public async Task<decimal> CurrentPercentageAsync(int employeeId, CancellationToken ct) =>
        await db.EmployeeDeductionRules.Where(r => r.EmployeeId == employeeId && r.EffectiveToUtc == null)
            .Select(r => (decimal?)r.Percentage).FirstOrDefaultAsync(ct) ?? 0;

    private async Task<Employee> LoadAsync(int id, CancellationToken ct) =>
        await db.Employees.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("الموظف غير موجود");

    private async Task EnsureUniqueAsync(string? username, string? email, int? exceptUserId, CancellationToken ct)
    {
        if (username is not null && await db.Users.AnyAsync(u => u.Username == username, ct))
            throw AppException.Conflict("اسم المستخدم مستخدم مسبقاً");
        if (email is not null && await db.Users.AnyAsync(u => u.Email == email && u.Id != exceptUserId, ct))
            throw AppException.Conflict("البريد الإلكتروني مستخدم مسبقاً");
    }

    private async Task<string> NextEmployeeCodeAsync(CancellationToken ct)
    {
        var count = await db.Employees.CountAsync(ct);
        string code;
        do { code = $"EMP-{++count:D4}"; } while (await db.Employees.AnyAsync(e => e.EmployeeCode == code, ct));
        return code;
    }

    private static EmployeeDetailDto ToDetail(Employee e, decimal pct) => new(
        e.Id, e.UserId, e.EmployeeCode, e.User.FullName, e.User.Username, e.User.Email, e.Phone, e.JobTitle, e.HireDate,
        e.User.IsActive, pct, e.User.LastLoginAtUtc, e.CreatedAtUtc, e.LoginSlug);
}
