using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Infrastructure;

namespace SalesSystem.Api.Controllers;

public record NotificationDto(string Id, string Title, string Detail, string Link, string Severity);

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(AppDbContext db, BusinessClock clock, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<NotificationDto>> List(CancellationToken ct)
    {
        var items = new List<NotificationDto>();
        if (currentUser.IsAdmin)
        {
            var pending = await db.SalesOrders.AsNoTracking().Where(o => o.Status == OrderStatus.Pending)
                .OrderBy(o => o.CreatedAtUtc).Take(5).Select(o => new { o.Id, o.OrderNumber }).ToListAsync(ct);
            items.AddRange(pending.Select(o => new NotificationDto($"order-{o.Id}", "طلب ينتظر الاعتماد", o.OrderNumber, $"/orders/{o.Id}", "warning")));
        }
        if (User.IsInRole(RoleNames.SuperAdmin) || User.IsInRole(RoleNames.OperationsManager) || User.IsInRole(RoleNames.Accountant))
        {
            var overdue = await db.Rentals.AsNoTracking().Where(r => !r.IsArchived && r.DueDate < clock.Today &&
                r.Amount > (r.Payments.Where(p => !p.IsVoided).Sum(p => (decimal?)p.Amount) ?? 0))
                .OrderBy(r => r.DueDate).Take(5).Select(r => new { r.Id, r.Title, r.DueDate }).ToListAsync(ct);
            items.AddRange(overdue.Select(r => new NotificationDto($"rental-{r.Id}", "إيجار متأخر", $"{r.Title} · {r.DueDate}", $"/rentals/{r.Id}", "danger")));
        }
        if (User.IsInRole(RoleNames.SuperAdmin) || User.IsInRole(RoleNames.Accountant))
        {
            var differences = await db.DailyCloses.AsNoTracking().Where(c => c.CountedCash != c.ExpectedCash || c.CountedCard != c.ExpectedCard)
                .OrderByDescending(c => c.BusinessDate).Take(3).Select(c => new { c.Id, c.BusinessDate }).ToListAsync(ct);
            items.AddRange(differences.Select(c => new NotificationDto($"close-{c.Id}", "فرق في الإقفال", c.BusinessDate.ToString(), "/daily-close", "warning")));
        }
        return items;
    }
}
