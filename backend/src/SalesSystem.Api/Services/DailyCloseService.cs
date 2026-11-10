using System.Data;
using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Infrastructure;

namespace SalesSystem.Api.Services;

public record DailyClosePreview(DateOnly BusinessDate, decimal ExpectedCash, decimal ExpectedCard, int OrdersCount, int RentalPaymentsCount, int PendingOrders, bool IsClosed);
public record DailyCloseRequest(DateOnly BusinessDate, decimal CountedCash, decimal CountedCard, string? Notes);

public class DailyCloseService(AppDbContext db, BusinessClock clock, ICurrentUser user, AuditService audit)
{
    public async Task<DailyClosePreview> PreviewAsync(DateOnly date, CancellationToken ct)
    {
        var orders = db.SalesOrders.AsNoTracking().Where(o => o.OperationDate == date);
        var activeOrders = orders.Where(o => o.Status == OrderStatus.Completed);
        var rentals = db.RentalPayments.AsNoTracking().Where(p => p.PaidOn == date && !p.IsVoided);
        var cash = await activeOrders.Where(o => o.PaymentMethod == PaymentMethod.Cash).SumAsync(o => (decimal?)o.Subtotal, ct) ?? 0;
        cash += await rentals.Where(p => p.Method == PaymentMethod.Cash).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var card = await activeOrders.Where(o => o.PaymentMethod == PaymentMethod.Card).SumAsync(o => (decimal?)o.Subtotal, ct) ?? 0;
        card += await rentals.Where(p => p.Method == PaymentMethod.Card).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        return new DailyClosePreview(date, cash, card, await activeOrders.CountAsync(ct), await rentals.CountAsync(ct),
            await orders.CountAsync(o => o.Status == OrderStatus.Pending, ct), await db.DailyCloses.AnyAsync(x => x.BusinessDate == date, ct));
    }

    public Task<List<DailyClose>> ListAsync(CancellationToken ct) =>
        db.DailyCloses.AsNoTracking().OrderByDescending(x => x.BusinessDate).Take(100).ToListAsync(ct);

    public async Task<DailyClose> CloseAsync(DailyCloseRequest req, CancellationToken ct)
    {
        if (req.BusinessDate >= clock.Today) throw AppException.BadRequest("يمكن إقفال الأيام السابقة فقط");
        if (req.CountedCash < 0 || req.CountedCard < 0) throw AppException.BadRequest("المبالغ الفعلية لا يمكن أن تكون سالبة");
        if (req.Notes?.Length > 1000) throw AppException.BadRequest("الملاحظة طويلة جداً");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var preview = await PreviewAsync(req.BusinessDate, ct);
        if (preview.IsClosed) throw AppException.Conflict("اليوم مقفل مسبقاً");
        if (preview.PendingOrders > 0) throw AppException.Conflict("اعتمد أو ألغِ الطلبات المعلقة قبل الإقفال");
        if ((preview.ExpectedCash != req.CountedCash || preview.ExpectedCard != req.CountedCard) && string.IsNullOrWhiteSpace(req.Notes))
            throw AppException.BadRequest("أضف سبباً لفرق الجرد");

        var close = new DailyClose
        {
            BusinessDate = req.BusinessDate, ExpectedCash = preview.ExpectedCash, ExpectedCard = preview.ExpectedCard,
            CountedCash = req.CountedCash, CountedCard = req.CountedCard, OrdersCount = preview.OrdersCount,
            RentalPaymentsCount = preview.RentalPaymentsCount, Notes = req.Notes?.Trim(), ClosedByUserId = user.UserId,
            ClosedAtUtc = clock.UtcNow,
        };
        db.DailyCloses.Add(close);
        audit.Add(AuditActions.Create, "DailyClose", req.BusinessDate.ToString(), new { close.ExpectedCash, close.ExpectedCard, close.CountedCash, close.CountedCard, close.Notes });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return close;
    }

    public async Task EnsureOpenAsync(DateOnly date, CancellationToken ct)
    {
        if (await db.DailyCloses.AnyAsync(x => x.BusinessDate == date, ct))
            throw AppException.Conflict("هذا اليوم مقفل مالياً ولا يمكن تعديل عملياته");
    }
}
