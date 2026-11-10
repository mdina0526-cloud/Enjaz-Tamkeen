using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClosedXML.Excel;
using SalesSystem.Api.Dtos;

namespace SalesSystem.Tests;

public class ProfessionalFeaturesTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task Role_change_revokes_token_and_limits_manager_and_accountant()
    {
        var admin = await app.AdminAsync();
        var employee = await app.CreateEmployeeAsync(admin, "role_user", 0);
        var old = await app.LoginAsync("role_user", TestApp.EmployeePassword);
        (await admin.PutAsJsonAsync($"/api/employees/{employee.Id}/role", new { role = "OperationsManager" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await old.GetAsync("/api/auth/me")).StatusCode);
        var manager = await app.LoginAsync("role_user", TestApp.EmployeePassword);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/dashboard/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/reports/Sales")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/settings/deduction")).StatusCode);

        (await admin.PutAsJsonAsync($"/api/employees/{employee.Id}/role", new { role = "Accountant" })).EnsureSuccessStatusCode();
        var accountant = await app.LoginAsync("role_user", TestApp.EmployeePassword);
        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync("/api/rentals")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync("/api/daily-close")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync("/api/orders")).StatusCode);
    }

    [Fact]
    public async Task Closing_a_day_records_variance_and_blocks_backdated_orders()
    {
        var admin = await app.AdminAsync();
        var employee = await app.CreateEmployeeAsync(admin, "close_user", 0);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));
        var create = await admin.PostAsJsonAsync("/api/orders", new
        {
            employeeId = employee.Id, operationDate = date,
            items = new[] { new { serviceTypeId = 1, quantity = 1, unitPrice = 100 } }, paymentMethod = "Cash",
        });
        create.EnsureSuccessStatusCode();

        var preview = await (await admin.GetAsync($"/api/daily-close/preview?date={date:yyyy-MM-dd}"))
            .ReadAsync<SalesSystem.Api.Services.DailyClosePreview>();
        Assert.Equal(100, preview.ExpectedCash);
        Assert.Equal(1, preview.OrdersCount);
        var missingReason = await admin.PostAsJsonAsync("/api/daily-close", new { businessDate = date, countedCash = 95, countedCard = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        (await admin.PostAsJsonAsync("/api/daily-close", new { businessDate = date, countedCash = 95, countedCard = 0, notes = "فرق جرد" })).EnsureSuccessStatusCode();
        var blocked = await admin.PostAsJsonAsync("/api/orders", new
        {
            employeeId = employee.Id, operationDate = date,
            items = new[] { new { serviceTypeId = 1, quantity = 1, unitPrice = 50 } }, paymentMethod = "Cash",
        });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
    }

    [Fact]
    public async Task Confirmed_mfa_requires_second_factor()
    {
        var admin = await app.AdminAsync();
        var employee = await app.CreateEmployeeAsync(admin, "mfa_user", 0);
        var account = await app.LoginAsync("mfa_user", TestApp.EmployeePassword);
        var setup = await (await account.PostAsJsonAsync("/api/auth/mfa/setup", new { currentPassword = TestApp.EmployeePassword }))
            .ReadAsync<MfaSetupDto>();
        var code = CurrentCode(setup.Secret);
        (await account.PostAsJsonAsync("/api/auth/mfa/confirm", new { code })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await account.GetAsync("/api/auth/me")).StatusCode);
        var withoutCode = await app.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "mfa_user", password = TestApp.EmployeePassword });
        Assert.Equal(HttpStatusCode.Unauthorized, withoutCode.StatusCode);
        (await admin.PostAsJsonAsync($"/api/employees/{employee.Id}/reset-password", new { newPassword = "FreshPass123" })).EnsureSuccessStatusCode();
        var recovered = await app.LoginAsync("mfa_user", "FreshPass123");
        Assert.Equal(HttpStatusCode.OK, (await recovered.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Excel_import_previews_then_applies_real_service_rows()
    {
        var admin = await app.AdminAsync();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Services");
        var headers = new[] { "Name", "Description", "DefaultPrice", "OrderTypeCode", "IsActive" };
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Cell(2, 1).Value = "خدمة مستوردة";
        sheet.Cell(2, 2).Value = "بيانات أصلية";
        sheet.Cell(2, 3).Value = 125;
        sheet.Cell(2, 4).Value = "Sales";
        sheet.Cell(2, 5).Value = true;
        using var file = new MemoryStream();
        workbook.SaveAs(file);
        MultipartFormDataContent Body()
        {
            var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(file.ToArray()), "file", "services.xlsx");
            return form;
        }
        using var previewBody = Body();
        var preview = await (await admin.PostAsync("/api/import/services/preview", previewBody)).ReadAsync<SalesSystem.Api.Controllers.ServiceImportPreview>();
        Assert.Empty(preview.Errors);
        Assert.Equal("create", Assert.Single(preview.Rows).Action);
        using var applyBody = Body();
        (await admin.PostAsync("/api/import/services/apply", applyBody)).EnsureSuccessStatusCode();
        var services = await (await admin.GetAsync("/api/catalog/services?includeInactive=true")).ReadAsync<List<ServiceTypeDto>>();
        Assert.Contains(services, s => s.Name == "خدمة مستوردة" && s.DefaultPrice == 125);
    }

    private static string CurrentCode(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var ch in base32)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(ch);
            bits += 5;
            if (bits >= 8) { bits -= 8; bytes.Add((byte)(buffer >> bits)); }
        }
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        Span<byte> message = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(message, step);
        var hash = HMACSHA1.HashData(bytes.ToArray(), message);
        var offset = hash[^1] & 15;
        var value = ((hash[offset] & 127) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6");
    }
}
