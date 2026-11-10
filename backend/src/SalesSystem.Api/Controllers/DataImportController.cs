using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Infrastructure;
using SalesSystem.Api.Services;

namespace SalesSystem.Api.Controllers;

public record ServiceImportRow(int Row, string Name, string? Description, decimal? DefaultPrice, string? OrderTypeCode, bool IsActive, string Action);
public record ServiceImportPreview(IReadOnlyList<ServiceImportRow> Rows, IReadOnlyList<string> Errors);

[ApiController]
[Route("api/import/services")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class DataImportController(AppDbContext db, AuditService audit) : ControllerBase
{
    [HttpGet("template")]
    public IActionResult Template()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Services");
        var headers = new[] { "Name", "Description", "DefaultPrice", "OrderTypeCode", "IsActive" };
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "services-import-template.xlsx");
    }

    [HttpPost("preview")]
    public Task<ServiceImportPreview> Preview([FromForm] IFormFile file, CancellationToken ct) => ParseAsync(file, ct);

    [HttpPost("apply")]
    public async Task<ServiceImportPreview> Apply([FromForm] IFormFile file, CancellationToken ct)
    {
        var preview = await ParseAsync(file, ct);
        if (preview.Errors.Count > 0) throw AppException.BadRequest("صحح أخطاء الملف قبل الاستيراد");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var types = await db.OrderTypes.ToDictionaryAsync(t => t.Code, StringComparer.OrdinalIgnoreCase, ct);
        var existing = await db.Services.ToDictionaryAsync(s => s.Name, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var row in preview.Rows)
        {
            var typeId = row.OrderTypeCode is null ? (int?)null : types[row.OrderTypeCode].Id;
            if (!existing.TryGetValue(row.Name, out var service))
            {
                service = new ServiceType { Name = row.Name };
                db.Services.Add(service);
                existing.Add(row.Name, service);
            }
            service.Description = row.Description;
            service.DefaultPrice = row.DefaultPrice;
            service.OrderTypeId = typeId;
            service.IsActive = row.IsActive;
        }
        audit.Add(AuditActions.Update, "ServiceImport", null, new { count = preview.Rows.Count });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return preview;
    }

    private async Task<ServiceImportPreview> ParseAsync(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0 || file.Length > 5 * 1024 * 1024 || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw AppException.BadRequest("اختر ملف Excel بصيغة xlsx بحجم لا يتجاوز 5 ميجابايت");
        var errors = new List<string>();
        var rows = new List<ServiceImportRow>();
        var typeCodes = await db.OrderTypes.AsNoTracking().Select(t => t.Code).ToListAsync(ct);
        var existingNames = await db.Services.AsNoTracking().Select(s => s.Name).ToListAsync(ct);
        using var stream = file.OpenReadStream();
        XLWorkbook workbook;
        try { workbook = new XLWorkbook(stream); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { throw AppException.BadRequest("ملف Excel تالف أو غير مدعوم"); }
        using (workbook)
        {
        var sheet = workbook.Worksheets.FirstOrDefault() ?? throw AppException.BadRequest("ملف Excel فارغ");
        var expected = new[] { "Name", "Description", "DefaultPrice", "OrderTypeCode", "IsActive" };
        if (expected.Where((header, i) => sheet.Cell(1, i + 1).GetString().Trim() != header).Any())
            throw AppException.BadRequest("الأعمدة المطلوبة: Name, Description, DefaultPrice, OrderTypeCode, IsActive");
        var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
        if (last > 1001) throw AppException.BadRequest("الحد الأقصى 1000 خدمة في الملف الواحد");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var rowNo = 2; rowNo <= last; rowNo++)
        {
            var name = sheet.Cell(rowNo, 1).GetString().Trim();
            var description = sheet.Cell(rowNo, 2).GetString().Trim();
            var priceText = sheet.Cell(rowNo, 3).GetString().Trim();
            var typeCode = sheet.Cell(rowNo, 4).GetString().Trim();
            var activeText = sheet.Cell(rowNo, 5).GetString().Trim();
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(description) && string.IsNullOrEmpty(priceText) && string.IsNullOrEmpty(typeCode)) continue;
            if (name.Length is < 2 or > 200 || description.Length > 500) errors.Add($"الصف {rowNo}: اسم أو وصف غير صالح");
            if (!seen.Add(name)) errors.Add($"الصف {rowNo}: اسم الخدمة مكرر في الملف");
            decimal? price = null;
            if (!string.IsNullOrEmpty(priceText))
            {
                if (!decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0 || parsed > 100_000_000)
                    errors.Add($"الصف {rowNo}: السعر غير صالح");
                else price = Math.Round(parsed, 2, MidpointRounding.AwayFromZero);
            }
            if (typeCode.Length > 0 && !typeCodes.Contains(typeCode, StringComparer.OrdinalIgnoreCase)) errors.Add($"الصف {rowNo}: رمز نوع الطلب غير موجود");
            if (activeText is not ("" or "1" or "0") && !activeText.Equals("true", StringComparison.OrdinalIgnoreCase) && !activeText.Equals("false", StringComparison.OrdinalIgnoreCase))
                errors.Add($"الصف {rowNo}: IsActive يجب أن يكون true أو false");
            var active = activeText is not "0" && !activeText.Equals("false", StringComparison.OrdinalIgnoreCase);
            rows.Add(new ServiceImportRow(rowNo, name, description.Length == 0 ? null : description, price, typeCode.Length == 0 ? null : typeCode,
                active, existingNames.Contains(name, StringComparer.OrdinalIgnoreCase) ? "update" : "create"));
        }
        if (rows.Count == 0) errors.Add("لا توجد خدمات في الملف");
        return new ServiceImportPreview(rows, errors);
        }
    }
}
