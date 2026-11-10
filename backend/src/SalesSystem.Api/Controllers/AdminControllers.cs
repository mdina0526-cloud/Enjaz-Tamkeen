using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Infrastructure;
using SalesSystem.Api.Services;

namespace SalesSystem.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet("admin")]
    [Authorize(Roles = RoleNames.Management)]
    public Task<AdminDashboardDto> Admin([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => dashboard.AdminAsync(from, to, ct);

    [HttpGet("employee")]
    [Authorize(Roles = $"{RoleNames.Employee},{RoleNames.Accountant}")]
    public Task<EmployeeDashboardDto> Employee(CancellationToken ct) => dashboard.EmployeeAsync(ct);
}

[ApiController]
[Route("api/reports")]
[Authorize(Roles = RoleNames.Finance)]
public class ReportsController(ReportService reports, ReportExporter exporter, AuditService audit, AppDbContext db) : ControllerBase
{
    [HttpGet("{type}")]
    public async Task<IActionResult> Get(ReportType type, [FromQuery] ReportQuery query, CancellationToken ct)
    {
        var report = await reports.BuildAsync(type, query, ct);
        var format = (query.Format ?? "json").ToLowerInvariant();
        if (format == "json") return Ok(report);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var fileBase = $"{type.ToString().ToLowerInvariant()}-report-{stamp}";
        audit.Add(AuditActions.Export, "Report", type.ToString(), new { format, query.From, query.To, query.EmployeeId });
        await db.SaveChangesAsync(ct);

        return format switch
        {
            "xlsx" => File(exporter.ToExcel(report), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileBase}.xlsx"),
            "pdf" => File(exporter.ToPdf(report), "application/pdf", $"{fileBase}.pdf"),
            _ => throw AppException.BadRequest("صيغة التصدير غير مدعومة (json, xlsx, pdf)"),
        };
    }
}

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class AuditLogsController(AppDbContext db, BusinessClock clock) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<AuditLogDto>> List([FromQuery] AuditQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Action)) q = q.Where(a => a.Action == query.Action);
        if (!string.IsNullOrWhiteSpace(query.EntityType)) q = q.Where(a => a.EntityType == query.EntityType);
        if (query.UserId.HasValue) q = q.Where(a => a.UserId == query.UserId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(a => (a.Username != null && a.Username.Contains(s)) || (a.EntityId != null && a.EntityId == s) || (a.Details != null && a.Details.Contains(s)));
        }
        var (f, t) = clock.UtcRange(query.From, query.To);
        if (query.From.HasValue) q = q.Where(a => a.CreatedAtUtc >= f);
        if (query.To.HasValue) q = q.Where(a => a.CreatedAtUtc < t);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.Id).Skip((page - 1) * size).Take(size)
            .Select(a => new AuditLogDto(a.Id, a.CreatedAtUtc, a.Username, a.Action, a.EntityType, a.EntityId, a.Details, a.IpAddress))
            .ToListAsync(ct);
        return new PagedResult<AuditLogDto>(items, total, page, size);
    }
}

[ApiController]
[Route("api/settings")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class SettingsController(SettingsService settings) : ControllerBase
{
    [HttpGet("deduction")]
    public Task<DeductionSettingsDto> GetDeduction(CancellationToken ct) => settings.GetDeductionSettingsAsync(ct);

    [HttpPut("deduction")]
    public Task<DeductionSettingsDto> UpdateDeduction(DeductionSettingsDto dto, CancellationToken ct) => settings.UpdateDeductionSettingsAsync(dto, ct);
}
