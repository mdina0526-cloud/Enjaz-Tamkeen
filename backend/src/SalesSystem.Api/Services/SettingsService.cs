using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Infrastructure;

namespace SalesSystem.Api.Services;

public class SettingsService(AppDbContext db, AuditService audit, BusinessClock clock)
{
    private const string KeyEnabled = "Deduction.Enabled";
    private const string KeyTreatment = "Deduction.Treatment";
    private const string KeyLabel = "Deduction.Label";

    public async Task<DeductionSettingsDto> GetDeductionSettingsAsync(CancellationToken ct = default)
    {
        var values = await db.SystemSettings.AsNoTracking()
            .Where(s => s.Key.StartsWith("Deduction."))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        return new DeductionSettingsDto(
            Enabled: !values.TryGetValue(KeyEnabled, out var en) || bool.Parse(en),
            Treatment: values.TryGetValue(KeyTreatment, out var tr) && Enum.TryParse<DeductionTreatment>(tr, out var t) ? t : DeductionTreatment.SubtractFromNet,
            Label: values.TryGetValue(KeyLabel, out var lb) ? lb : "استقطاع");
    }

    public async Task<DeductionSettingsDto> UpdateDeductionSettingsAsync(DeductionSettingsDto dto, CancellationToken ct = default)
    {
        var before = await GetDeductionSettingsAsync(ct);
        await UpsertAsync(KeyEnabled, dto.Enabled.ToString(), ct);
        await UpsertAsync(KeyTreatment, dto.Treatment.ToString(), ct);
        await UpsertAsync(KeyLabel, dto.Label.Trim(), ct);
        audit.Add(AuditActions.Update, "SystemSettings", "Deduction", new { before, after = dto });
        await db.SaveChangesAsync(ct);
        return await GetDeductionSettingsAsync(ct);
    }

    private async Task UpsertAsync(string key, string value, CancellationToken ct)
    {
        var row = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null) db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, UpdatedAtUtc = clock.UtcNow });
        else { row.Value = value; row.UpdatedAtUtc = clock.UtcNow; }
    }
}
