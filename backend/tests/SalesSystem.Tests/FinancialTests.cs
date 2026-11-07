using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;

namespace SalesSystem.Tests;

public class FinancialTests(TestApp app) : IClassFixture<TestApp>
{
    private T WithDb<T>(Func<AppDbContext, T> f)
    {
        using var scope = app.Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task Order_of_1000_with_5_percent_stores_50_deduction_and_950_net()
    {
        var admin = await app.AdminAsync();
        await app.CreateEmployeeAsync(admin, "fin_5", 5);
        var emp = await app.LoginAsync("fin_5", TestApp.EmployeePassword);

        var order = await TestApp.CreateOrderAsync(emp, 1000);

        Assert.Equal(1000m, order.Subtotal);
        Assert.Equal(5m, order.DeductionPercentage);
        Assert.Equal(50m, order.DeductionAmount);
        Assert.Equal(950m, order.NetAmount);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Matches(@"^SO-\d{8}-\d{6}$", order.OrderNumber);
        var payment = Assert.Single(order.Payments);
        Assert.Equal(1000m, payment.Amount);

        var record = WithDb(db => db.DeductionRecords.Single(d => d.SalesOrderId == order.Id));
        Assert.Equal(50m, record.DeductionAmount);
        Assert.Equal(950m, record.NetAmount);
        Assert.Equal(5m, record.Percentage);
    }

    [Fact]
    public async Task Multiple_items_are_totalled_on_the_server()
    {
        var admin = await app.AdminAsync();
        await app.CreateEmployeeAsync(admin, "fin_multi", 10);
        var emp = await app.LoginAsync("fin_multi", TestApp.EmployeePassword);

        var order = await (await emp.PostAsJsonAsync("/api/orders", new
        {
            items = new object[]
            {
                new { serviceTypeId = 1, quantity = 2, unitPrice = 150.25m },
                new { serviceTypeId = 2, quantity = 1, unitPrice = 99.50m },
            },
            paymentMethod = "Card",
            notes = "طلب متعدد",
        })).ReadAsync<OrderDetailDto>();

        Assert.Equal(400.00m, order.Subtotal);      // 300.50 + 99.50
        Assert.Equal(40.00m, order.DeductionAmount);
        Assert.Equal(360.00m, order.NetAmount);
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(PaymentMethod.Card, order.PaymentMethod);
    }

    [Fact]
    public async Task Changing_the_rate_does_not_alter_past_orders()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "fin_rate", 5);
        var emp = await app.LoginAsync("fin_rate", TestApp.EmployeePassword);
        var before = await TestApp.CreateOrderAsync(emp, 1000);

        (await admin.PutAsJsonAsync($"/api/employees/{e.Id}/deduction", new { percentage = 10, notes = "تعديل" })).EnsureSuccessStatusCode();
        var after = await TestApp.CreateOrderAsync(emp, 1000);

        var reloaded = await (await admin.GetAsync($"/api/orders/{before.Id}")).ReadAsync<OrderDetailDto>();
        Assert.Equal(5m, reloaded.DeductionPercentage);
        Assert.Equal(50m, reloaded.DeductionAmount);
        Assert.Equal(10m, after.DeductionPercentage);
        Assert.Equal(100m, after.DeductionAmount);

        var history = await (await admin.GetAsync($"/api/employees/{e.Id}/deduction-history")).ReadAsync<List<DeductionRuleDto>>();
        Assert.Equal(2, history.Count);
        Assert.Single(history, h => h.EffectiveToUtc == null);
    }

    [Fact]
    public async Task Record_only_setting_keeps_net_equal_to_gross()
    {
        var admin = await app.AdminAsync();
        await app.CreateEmployeeAsync(admin, "fin_ro", 5);
        (await admin.PutAsJsonAsync("/api/settings/deduction", new { enabled = true, treatment = "RecordOnly", label = "عمولة" })).EnsureSuccessStatusCode();
        try
        {
            var emp = await app.LoginAsync("fin_ro", TestApp.EmployeePassword);
            var order = await TestApp.CreateOrderAsync(emp, 1000);
            Assert.Equal(50m, order.DeductionAmount);
            Assert.Equal(1000m, order.NetAmount);
            Assert.Equal(DeductionTreatment.RecordOnly, order.DeductionTreatment);
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/settings/deduction", new { enabled = true, treatment = "SubtractFromNet", label = "استقطاع" })).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Retried_request_with_same_idempotency_key_creates_one_order_and_one_payment()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "fin_idem", 0);
        var emp = await app.LoginAsync("fin_idem", TestApp.EmployeePassword);
        var key = Guid.NewGuid().ToString();

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => TestApp.CreateOrderAsync(emp, 250, key: key)));

        Assert.Single(results.Select(r => r.Id).Distinct());
        var counts = WithDb(db => new
        {
            Orders = db.SalesOrders.Count(o => o.EmployeeId == e.Id),
            Payments = db.Payments.Count(p => p.SalesOrder.EmployeeId == e.Id),
        });
        Assert.Equal(1, counts.Orders);
        Assert.Equal(1, counts.Payments);
    }

    [Fact]
    public async Task Cancelling_keeps_records_voids_payment_and_writes_audit_log()
    {
        var admin = await app.AdminAsync();
        await app.CreateEmployeeAsync(admin, "fin_cancel", 5);
        var emp = await app.LoginAsync("fin_cancel", TestApp.EmployeePassword);
        var order = await TestApp.CreateOrderAsync(emp, 500);

        var cancelled = await (await admin.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new { reason = "خطأ في الإدخال" })).ReadAsync<OrderDetailDto>();
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal("خطأ في الإدخال", cancelled.CancellationReason);
        Assert.All(cancelled.Payments, p => Assert.Equal(PaymentStatus.Voided, p.Status));

        // Second cancel is rejected; nothing was deleted.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new { reason = "again" })).StatusCode);
        var state = WithDb(db => new
        {
            Exists = db.SalesOrders.Any(o => o.Id == order.Id),
            Payments = db.Payments.Count(p => p.SalesOrderId == order.Id),
            Voided = db.DeductionRecords.Single(d => d.SalesOrderId == order.Id).IsVoided,
            Audit = db.AuditLogs.Count(a => a.EntityType == "SalesOrder" && a.EntityId == order.Id.ToString() && a.Action == "Cancel"),
        });
        Assert.True(state.Exists);
        Assert.Equal(1, state.Payments);
        Assert.True(state.Voided);
        Assert.Equal(1, state.Audit);

        // Cancelled orders are excluded from the employee's totals.
        var dash = await (await emp.GetAsync("/api/dashboard/employee")).ReadAsync<EmployeeDashboardDto>();
        Assert.Equal(0, dash.OrdersCount);
        Assert.Equal(1, dash.CancelledOrders);
    }

    [Fact]
    public async Task Admin_approves_pending_order_and_dashboards_reflect_totals()
    {
        var admin = await app.AdminAsync();
        await app.CreateEmployeeAsync(admin, "fin_dash", 10);
        var emp = await app.LoginAsync("fin_dash", TestApp.EmployeePassword);
        var o1 = await TestApp.CreateOrderAsync(emp, 1000, "Cash");
        await TestApp.CreateOrderAsync(emp, 500, "Card");

        var approved = await (await admin.PostAsync($"/api/orders/{o1.Id}/approve", null)).ReadAsync<OrderDetailDto>();
        Assert.Equal(OrderStatus.Completed, approved.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/orders/{o1.Id}/approve", null)).StatusCode);

        var dash = await (await emp.GetAsync("/api/dashboard/employee")).ReadAsync<EmployeeDashboardDto>();
        Assert.Equal(2, dash.OrdersCount);
        Assert.Equal(2, dash.TodayOrders);
        Assert.Equal(1500m, dash.TotalSales);
        Assert.Equal(150m, dash.TotalDeductions);
        Assert.Equal(1350m, dash.TotalNet);
        Assert.Equal(1, dash.PendingOrders);
        Assert.Equal(10m, dash.DeductionPercentage);
    }

    [Fact]
    public async Task Admin_order_requires_an_employee_and_is_completed_immediately()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "fin_adm", 0);
        var missing = await admin.PostAsJsonAsync("/api/orders", new { items = new[] { new { serviceTypeId = 1, quantity = 1, unitPrice = 10 } }, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var order = await TestApp.CreateOrderAsync(admin, 75, employeeId: e.Id);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(e.Id, order.EmployeeId);
    }

    [Fact]
    public async Task Rental_payments_track_status_and_reject_overpayment()
    {
        var admin = await app.AdminAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var rental = await (await admin.PostAsJsonAsync("/api/rentals", new
        {
            title = "مكتب اختبار", propertyType = "Office", beneficiaryName = "مالك", amount = 3000, dueDate = today.AddDays(20).ToString("yyyy-MM-dd"),
        })).ReadAsync<RentalDetailDto>();
        Assert.Equal(RentalStatus.Upcoming, rental.Status);

        async Task<HttpResponseMessage> Pay(decimal amount, string? key = null, (string Name, byte[] Bytes)? file = null)
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent(amount.ToString(System.Globalization.CultureInfo.InvariantCulture)), "amount" },
                { new StringContent("Cash"), "method" },
                { new StringContent(today.ToString("yyyy-MM-dd")), "paidOn" },
            };
            if (file is { } f) form.Add(new ByteArrayContent(f.Bytes), "attachment", f.Name);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/rentals/{rental.Id}/payments") { Content = form };
            if (key is not null) req.Headers.Add("Idempotency-Key", key);
            return await admin.SendAsync(req);
        }

        var pdf = "%PDF-1.4 test receipt"u8.ToArray();
        var key = Guid.NewGuid().ToString();
        var partial = await (await Pay(1000, key, ("receipt.pdf", pdf))).ReadAsync<RentalDetailDto>();
        Assert.Equal(RentalStatus.PartiallyPaid, partial.Status);
        Assert.Equal(2000m, partial.RemainingAmount);
        Assert.True(partial.Payments[0].HasAttachment);

        // Same key again → no second payment.
        var replay = await (await Pay(1000, key)).ReadAsync<RentalDetailDto>();
        Assert.Single(replay.Payments);

        Assert.Equal(HttpStatusCode.BadRequest, (await Pay(2500)).StatusCode);   // exceeds remaining
        Assert.Equal(HttpStatusCode.BadRequest, (await Pay(10, file: ("fake.pdf", "not a pdf"u8.ToArray()))).StatusCode);

        var paid = await (await Pay(2000)).ReadAsync<RentalDetailDto>();
        Assert.Equal(RentalStatus.Paid, paid.Status);
        Assert.Equal(0m, paid.RemainingAmount);

        var attachment = await admin.GetAsync($"/api/rentals/{rental.Id}/payments/{partial.Payments[0].Id}/attachment");
        Assert.Equal(HttpStatusCode.OK, attachment.StatusCode);
        Assert.Equal("application/pdf", attachment.Content.Headers.ContentType?.MediaType);

        var voided = await (await admin.PostAsJsonAsync($"/api/rentals/{rental.Id}/payments/{paid.Payments[0].Id}/void", new { reason = "دفعة خاطئة" })).ReadAsync<RentalDetailDto>();
        Assert.Equal(RentalStatus.PartiallyPaid, voided.Status);
        Assert.Equal(2, voided.Payments.Count);
    }

    [Fact]
    public async Task Overdue_rental_is_flagged()
    {
        var admin = await app.AdminAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var rental = await (await admin.PostAsJsonAsync("/api/rentals", new
        {
            title = "مستودع متأخر", propertyType = "Property", beneficiaryName = "مالك", amount = 500, dueDate = today.AddDays(-3).ToString("yyyy-MM-dd"),
        })).ReadAsync<RentalDetailDto>();
        Assert.Equal(RentalStatus.Overdue, rental.Status);

        var overdue = await (await admin.GetAsync("/api/rentals?status=Overdue")).ReadAsync<List<RentalListItemDto>>();
        Assert.Contains(overdue, r => r.Id == rental.Id);
    }

    [Theory]
    [InlineData("Sales")]
    [InlineData("Employees")]
    [InlineData("Cash")]
    [InlineData("Card")]
    [InlineData("Deductions")]
    [InlineData("Rentals")]
    [InlineData("Operations")]
    [InlineData("Performance")]
    [InlineData("Comprehensive")]
    public async Task Reports_build_and_export_to_excel_and_pdf(string type)
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, $"rep_{type.ToLowerInvariant()}", 5);
        await TestApp.CreateOrderAsync(admin, 1000, type == "Card" ? "Card" : "Cash", e.Id);

        var json = await (await admin.GetAsync($"/api/reports/{type}")).ReadAsync<ReportResult>();
        Assert.NotEmpty(json.Summary);
        Assert.NotEmpty(json.Sections);

        var xlsx = await admin.GetAsync($"/api/reports/{type}?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        var xlsxBytes = await xlsx.Content.ReadAsByteArrayAsync();
        Assert.Equal((byte)'P', xlsxBytes[0]); // zip container
        Assert.Equal((byte)'K', xlsxBytes[1]);

        var pdf = await admin.GetAsync($"/api/reports/{type}?format=pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..4]));
    }

    [Fact]
    public async Task Deduction_report_totals_match_orders()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "rep_totals", 5);
        await TestApp.CreateOrderAsync(admin, 1000, employeeId: e.Id);
        await TestApp.CreateOrderAsync(admin, 333.33m, employeeId: e.Id);

        var report = await (await admin.GetAsync($"/api/reports/Deductions?employeeId={e.Id}")).ReadAsync<ReportResult>();
        Assert.Contains(report.Summary, s => s.Value == "66.67");   // 50.00 + 16.67
        Assert.Contains(report.Summary, s => s.Value == "1,266.66"); // 950.00 + 316.66
    }
}
