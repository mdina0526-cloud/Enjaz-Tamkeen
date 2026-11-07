using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Infrastructure;

namespace SalesSystem.Api.Services;

public class DashboardService(AppDbContext db, BusinessClock clock, ICurrentUser currentUser, RentalService rentals, SettingsService settings, EmployeeService employees)
{
    private IQueryable<SalesOrder> InRange(DateOnly? from, DateOnly? to)
    {
        var q = db.SalesOrders.AsNoTracking();
        if (from.HasValue) q = q.Where(o => o.OperationDate >= from.Value);
        if (to.HasValue) q = q.Where(o => o.OperationDate <= to.Value);
        return q;
    }

    public async Task<AdminDashboardDto> AdminAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from > to) throw AppException.BadRequest("تاريخ البداية يجب أن يسبق تاريخ النهاية");
        var active = InRange(from, to).Where(o => o.Status != OrderStatus.Cancelled);
        var totals = await TotalsAsync(active, ct);

        var rentalPayments = db.RentalPayments.AsNoTracking().Where(p => !p.IsVoided);
        if (from.HasValue) rentalPayments = rentalPayments.Where(p => p.PaidOn >= from.Value);
        if (to.HasValue) rentalPayments = rentalPayments.Where(p => p.PaidOn <= to.Value);

        var rentalSummary = await rentals.SummaryAsync(new RentalQuery { From = from, To = to }, ct);
        var allRentals = from.HasValue || to.HasValue ? await rentals.SummaryAsync(new RentalQuery(), ct) : rentalSummary;

        // Same-length window immediately before the selected one, for period-over-period comparison.
        PeriodTotalsDto? previous = null;
        if (from.HasValue && to.HasValue)
        {
            var days = to.Value.DayNumber - from.Value.DayNumber + 1;
            var pTo = from.Value.AddDays(-1);
            var pFrom = pTo.AddDays(1 - days);
            var p = await TotalsAsync(InRange(pFrom, pTo).Where(o => o.Status != OrderStatus.Cancelled), ct);
            previous = new PeriodTotalsDto(pFrom, pTo, p.Sales, p.Count, p.Collected, p.Deductions, p.Net);
        }

        var performance = await active.GroupBy(o => new { o.EmployeeId, o.Employee.User.FullName, o.Employee.EmployeeCode })
            .Select(g => new EmployeePerformanceDto(g.Key.EmployeeId, g.Key.FullName, g.Key.EmployeeCode, g.Count(),
                g.Sum(o => o.Subtotal), g.Sum(o => o.DeductionAmount), g.Sum(o => o.NetAmount)))
            .ToListAsync(ct);
        performance = performance.OrderByDescending(x => x.TotalSales).ToList();

        var byService = await db.SalesOrderItems.AsNoTracking()
            .Where(i => i.SalesOrder.Status != OrderStatus.Cancelled
                        && (!from.HasValue || i.SalesOrder.OperationDate >= from.Value)
                        && (!to.HasValue || i.SalesOrder.OperationDate <= to.Value))
            .GroupBy(i => i.ServiceType.Name)
            .Select(g => new KeyValueAmount(g.Key, g.Sum(i => i.LineTotal), g.Select(i => i.SalesOrderId).Distinct().Count()))
            .ToListAsync(ct);

        return new AdminDashboardDto(
            TotalSales: totals.Sales,
            OrdersCount: totals.Count,
            TotalCollected: totals.Collected,
            TotalDeductions: totals.Deductions,
            TotalNet: totals.Net,
            TotalRentalPayments: await rentalPayments.SumAsync(p => (decimal?)p.Amount, ct) ?? 0,
            TotalRentals: rentalSummary.TotalAmount,
            RentalsOutstanding: allRentals.TotalRemaining,
            ActiveEmployees: await db.Employees.CountAsync(e => e.User.IsActive, ct),
            PendingOrders: await db.SalesOrders.CountAsync(o => o.Status == OrderStatus.Pending, ct),
            OverdueRentals: allRentals.Overdue,
            Previous: previous,
            DailySales: await DailyAsync(db.SalesOrders.AsNoTracking(), to, ct),
            MonthlySales: await MonthlyAsync(db.SalesOrders.AsNoTracking(), to, ct),
            ByEmployee: performance.Select(p => new KeyValueAmount(p.EmployeeName, p.TotalSales, p.OrdersCount)).ToList(),
            ByPaymentMethod: await ByMethodAsync(active, ct),
            ByService: byService.OrderByDescending(x => x.Amount).ToList(),
            EmployeePerformance: performance,
            RecentOrders: await db.SalesOrders.AsNoTracking().OrderByDescending(o => o.CreatedAtUtc).ThenByDescending(o => o.Id).Take(8)
                .Select(OrderService.ToListItem).ToListAsync(ct));
    }

    public async Task<EmployeeDashboardDto> EmployeeAsync(CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId ?? throw AppException.Forbidden();
        var mine = db.SalesOrders.AsNoTracking().Where(o => o.EmployeeId == employeeId);
        var active = mine.Where(o => o.Status != OrderStatus.Cancelled);
        var todayDate = clock.Today;
        var today = active.Where(o => o.OperationDate == todayDate);
        var totals = await TotalsAsync(active, ct);
        var s = await settings.GetDeductionSettingsAsync(ct);

        return new EmployeeDashboardDto(
            TodayOrders: await today.CountAsync(ct),
            TodaySales: await today.SumAsync(o => (decimal?)o.Subtotal, ct) ?? 0,
            OrdersCount: totals.Count,
            TotalSales: totals.Sales,
            TotalCollected: totals.Collected,
            TotalDeductions: totals.Deductions,
            TotalNet: totals.Net,
            PendingOrders: await mine.CountAsync(o => o.Status == OrderStatus.Pending, ct),
            CompletedOrders: await mine.CountAsync(o => o.Status == OrderStatus.Completed, ct),
            CancelledOrders: await mine.CountAsync(o => o.Status == OrderStatus.Cancelled, ct),
            DeductionPercentage: await employees.CurrentPercentageAsync(employeeId, ct),
            DeductionLabel: s.Label,
            DailySales: await DailyAsync(mine, null, ct),
            ByPaymentMethod: await ByMethodAsync(active, ct),
            RecentOrders: await mine.OrderByDescending(o => o.CreatedAtUtc).ThenByDescending(o => o.Id).Take(8).Select(OrderService.ToListItem).ToListAsync(ct));
    }

    private readonly record struct Totals(decimal Sales, int Count, decimal Collected, decimal Deductions, decimal Net);

    /// <summary>Totals over non-cancelled orders. "Collected" counts only orders the admin has approved (Completed).</summary>
    private static async Task<Totals> TotalsAsync(IQueryable<SalesOrder> active, CancellationToken ct)
    {
        var row = await active.GroupBy(_ => 1).OrderBy(g => g.Key).Select(g => new
        {
            Sales = g.Sum(o => o.Subtotal),
            Count = g.Count(),
            Collected = g.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.Subtotal),
            Deductions = g.Sum(o => o.DeductionAmount),
            Net = g.Sum(o => o.NetAmount),
        }).FirstOrDefaultAsync(ct);
        return row is null ? default : new Totals(row.Sales, row.Count, row.Collected, row.Deductions, row.Net);
    }

    private static async Task<IReadOnlyList<KeyValueAmount>> ByMethodAsync(IQueryable<SalesOrder> q, CancellationToken ct)
    {
        var rows = await q.GroupBy(o => o.PaymentMethod).Select(g => new { g.Key, Amount = g.Sum(o => o.Subtotal), Count = g.Count() }).ToListAsync(ct);
        return Enum.GetValues<PaymentMethod>()
            .Select(m => rows.FirstOrDefault(r => r.Key == m) is { } r ? new KeyValueAmount(m.ToString(), r.Amount, r.Count) : new KeyValueAmount(m.ToString(), 0, 0))
            .ToList();
    }

    /// <summary>Last 30 business days ending at <paramref name="end"/> (or today).</summary>
    private async Task<IReadOnlyList<KeyValueAmount>> DailyAsync(IQueryable<SalesOrder> q, DateOnly? end, CancellationToken ct)
    {
        var last = end ?? clock.Today;
        var first = last.AddDays(-29);
        var grouped = await q.Where(o => o.Status != OrderStatus.Cancelled && o.OperationDate >= first && o.OperationDate <= last)
            .GroupBy(o => o.OperationDate)
            .Select(g => new { g.Key, Sum = g.Sum(o => o.Subtotal), Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, ct);

        return Enumerable.Range(0, 30).Select(i => first.AddDays(i))
            .Select(d => grouped.TryGetValue(d, out var v)
                ? new KeyValueAmount(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), v.Sum, v.Count)
                : new KeyValueAmount(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 0, 0))
            .ToList();
    }

    /// <summary>Last 12 months ending at the month of <paramref name="end"/> (or today).</summary>
    private async Task<IReadOnlyList<KeyValueAmount>> MonthlyAsync(IQueryable<SalesOrder> q, DateOnly? end, CancellationToken ct)
    {
        var last = end ?? clock.Today;
        var firstMonth = new DateOnly(last.Year, last.Month, 1).AddMonths(-11);
        var lastDay = new DateOnly(last.Year, last.Month, 1).AddMonths(1).AddDays(-1);
        var rows = await q.Where(o => o.Status != OrderStatus.Cancelled && o.OperationDate >= firstMonth && o.OperationDate <= lastDay)
            .GroupBy(o => new { o.OperationDate.Year, o.OperationDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Sum = g.Sum(o => o.Subtotal), Count = g.Count() })
            .ToListAsync(ct);
        var grouped = rows.ToDictionary(r => $"{r.Year:D4}-{r.Month:D2}");

        return Enumerable.Range(0, 12).Select(i => firstMonth.AddMonths(i).ToString("yyyy-MM", CultureInfo.InvariantCulture))
            .Select(k => grouped.TryGetValue(k, out var v) ? new KeyValueAmount(k, v.Sum, v.Count) : new KeyValueAmount(k, 0, 0))
            .ToList();
    }
}
