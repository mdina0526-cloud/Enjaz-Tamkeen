using System.Net;
using System.Net.Http.Json;
using SalesSystem.Api.Dtos;

namespace SalesSystem.Tests;

public class CatalogAndProfileTests(TestApp app) : IClassFixture<TestApp>
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    private static object Order(int serviceTypeId, decimal amount, int orderTypeId = 1, DateOnly? date = null, int? employeeId = null, string method = "Cash") => new
    {
        orderTypeId,
        operationDate = date?.ToString("yyyy-MM-dd"),
        items = new[] { new { serviceTypeId, description = "وصف", unitPrice = amount } },
        paymentMethod = method,
        employeeId,
    };

    [Fact]
    public async Task Lookups_are_seeded_and_readable_by_employees_but_only_admins_can_change_them()
    {
        var admin = await app.AdminAsync();
        await app.CreateEmployeeAsync(admin, "cat_reader", 0);
        var emp = await app.LoginAsync("cat_reader", TestApp.EmployeePassword);

        var methods = await (await emp.GetAsync("/api/catalog/payment-methods")).ReadAsync<List<PaymentMethodDto>>();
        Assert.Equal(["Cash", "POS"], methods.Select(m => m.Code));
        var types = await (await emp.GetAsync("/api/catalog/order-types")).ReadAsync<List<OrderTypeDto>>();
        Assert.Contains(types, t => t.Code == "Sales");
        Assert.NotEmpty(await (await emp.GetAsync("/api/catalog/services")).ReadAsync<List<ServiceTypeDto>>());

        Assert.Equal(HttpStatusCode.Forbidden, (await emp.PostAsJsonAsync("/api/catalog/services", new { name = "خدمة موظف" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.PostAsJsonAsync("/api/catalog/order-types", new { code = "Hack", name = "اختراق" })).StatusCode);
    }

    [Fact]
    public async Task New_order_types_and_services_are_usable_and_validated()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "cat_user", 10);
        var emp = await app.LoginAsync("cat_user", TestApp.EmployeePassword);

        var type = await (await admin.PostAsJsonAsync("/api/catalog/order-types", new { code = "Maintenance", name = "صيانة", sortOrder = 2 })).ReadAsync<OrderTypeDto>();
        var service = await (await admin.PostAsJsonAsync("/api/catalog/services", new { name = "صيانة دورية", defaultPrice = 300, orderTypeId = type.Id })).ReadAsync<ServiceTypeDto>();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/catalog/services", new { name = "صيانة دورية" })).StatusCode);

        var order = await (await emp.PostAsJsonAsync("/api/orders", Order(service.Id, 300, type.Id))).ReadAsync<OrderDetailDto>();
        Assert.Equal("صيانة", order.OrderTypeName);
        Assert.Equal("صيانة دورية", order.Items[0].ServiceName);
        Assert.Equal(30m, order.DeductionAmount);
        Assert.Equal(e.Id, order.EmployeeId);

        // A service bound to another order type is rejected.
        Assert.Equal(HttpStatusCode.BadRequest, (await emp.PostAsJsonAsync("/api/orders", Order(service.Id, 300, orderTypeId: 1))).StatusCode);

        // Deactivated services cannot be used, and renaming does not rewrite history.
        (await admin.PutAsJsonAsync($"/api/catalog/services/{service.Id}", new { name = "صيانة شاملة", orderTypeId = type.Id, isActive = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await emp.PostAsJsonAsync("/api/orders", Order(service.Id, 300, type.Id))).StatusCode);
        var reloaded = await (await emp.GetAsync($"/api/orders/{order.Id}")).ReadAsync<OrderDetailDto>();
        Assert.Equal("صيانة دورية", reloaded.Items[0].ServiceName);

        // Employees never see inactive entries, even when asking for them.
        var visible = await (await emp.GetAsync("/api/catalog/services?includeInactive=true")).ReadAsync<List<ServiceTypeDto>>();
        Assert.DoesNotContain(visible, s => s.Id == service.Id);

        // Filtering by order type and by service.
        var byType = await (await admin.GetAsync($"/api/orders?orderTypeId={type.Id}")).ReadAsync<PagedResult<OrderListItemDto>>();
        Assert.All(byType.Items, o => Assert.Equal(type.Id, o.OrderTypeId));
        Assert.Contains(byType.Items, o => o.Id == order.Id);
    }

    [Fact]
    public async Task Invalid_payment_method_or_order_type_is_rejected()
    {
        var admin = await app.AdminAsync();
        await app.CreateEmployeeAsync(admin, "cat_invalid", 0);
        var emp = await app.LoginAsync("cat_invalid", TestApp.EmployeePassword);

        Assert.Equal(HttpStatusCode.BadRequest, (await emp.PostAsJsonAsync("/api/orders", Order(1, 100, orderTypeId: 999))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await emp.PostAsJsonAsync("/api/orders", Order(999, 100))).StatusCode);
        var badMethod = await emp.PostAsJsonAsync("/api/orders", new { items = new[] { new { serviceTypeId = 1, unitPrice = 10 } }, paymentMethod = 99 });
        Assert.Equal(HttpStatusCode.BadRequest, badMethod.StatusCode);
    }

    [Fact]
    public async Task Operation_date_cannot_be_in_the_future_and_employees_cannot_backdate_far()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "cat_dates", 0);
        var emp = await app.LoginAsync("cat_dates", TestApp.EmployeePassword);

        Assert.Equal(HttpStatusCode.BadRequest, (await emp.PostAsJsonAsync("/api/orders", Order(1, 100, date: Today.AddDays(1)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await emp.PostAsJsonAsync("/api/orders", Order(1, 100, date: Today.AddDays(-30)))).StatusCode);

        var recent = await (await emp.PostAsJsonAsync("/api/orders", Order(1, 100, date: Today.AddDays(-2)))).ReadAsync<OrderDetailDto>();
        Assert.Equal(Today.AddDays(-2), recent.OperationDate);

        var old = await (await admin.PostAsJsonAsync("/api/orders", Order(1, 100, date: Today.AddDays(-30), employeeId: e.Id))).ReadAsync<OrderDetailDto>();
        Assert.Equal(Today.AddDays(-30), old.OperationDate);

        // Date filters use the operation date, not the creation timestamp.
        var d = Today.AddDays(-30).ToString("yyyy-MM-dd");
        var filtered = await (await admin.GetAsync($"/api/orders?employeeId={e.Id}&from={d}&to={d}")).ReadAsync<PagedResult<OrderListItemDto>>();
        Assert.Equal(old.Id, Assert.Single(filtered.Items).Id);
    }

    [Fact]
    public async Task Orders_can_be_sorted_by_amount()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "cat_sort", 0);
        foreach (var amount in new[] { 300m, 100m, 200m })
            await TestApp.CreateOrderAsync(admin, amount, employeeId: e.Id);

        var asc = await (await admin.GetAsync($"/api/orders?employeeId={e.Id}&sortBy=amount&sortDir=asc")).ReadAsync<PagedResult<OrderListItemDto>>();
        Assert.Equal([100m, 200m, 300m], asc.Items.Select(o => o.Subtotal));
        var desc = await (await admin.GetAsync($"/api/orders?employeeId={e.Id}&sortBy=amount")).ReadAsync<PagedResult<OrderListItemDto>>();
        Assert.Equal([300m, 200m, 100m], desc.Items.Select(o => o.Subtotal));
    }

    [Fact]
    public async Task Admin_dashboard_compares_with_the_previous_period()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "cat_cmp", 5);
        // A distant, otherwise empty window keeps this test independent of other tests' orders.
        await (await admin.PostAsJsonAsync("/api/orders", Order(1, 400, date: new DateOnly(2001, 1, 5), employeeId: e.Id))).ReadAsync<OrderDetailDto>();
        await (await admin.PostAsJsonAsync("/api/orders", Order(1, 1000, date: new DateOnly(2001, 1, 12), employeeId: e.Id, method: "Card"))).ReadAsync<OrderDetailDto>();

        var dash = await (await admin.GetAsync("/api/dashboard/admin?from=2001-01-08&to=2001-01-14")).ReadAsync<AdminDashboardDto>();
        Assert.Equal(1000m, dash.TotalSales);
        Assert.Equal(1000m, dash.TotalCollected);
        Assert.Equal(50m, dash.TotalDeductions);
        Assert.NotNull(dash.Previous);
        Assert.Equal(new DateOnly(2001, 1, 1), dash.Previous!.From);
        Assert.Equal(new DateOnly(2001, 1, 7), dash.Previous.To);
        Assert.Equal(400m, dash.Previous.TotalSales);
        Assert.Equal(1, dash.Previous.OrdersCount);
        var perf = Assert.Single(dash.EmployeePerformance);
        Assert.Equal(950m, perf.TotalNet);
        Assert.Contains(dash.ByService, s => s.Amount == 1000m);
        Assert.Equal(1000m, dash.ByPaymentMethod.Single(m => m.Key == "Card").Amount);
    }

    [Fact]
    public async Task Performance_report_groups_by_period_and_validates_grouping()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "cat_perf", 0);
        await (await admin.PostAsJsonAsync("/api/orders", Order(1, 100, date: new DateOnly(2002, 3, 1), employeeId: e.Id))).ReadAsync<OrderDetailDto>();
        await (await admin.PostAsJsonAsync("/api/orders", Order(1, 300, date: new DateOnly(2002, 4, 1), employeeId: e.Id))).ReadAsync<OrderDetailDto>();

        var report = await (await admin.GetAsync($"/api/reports/Performance?from=2002-01-01&to=2002-12-31&employeeId={e.Id}&groupBy=month")).ReadAsync<ReportResult>();
        var rows = report.Sections[0].Rows;
        Assert.Equal(2, rows.Count);
        Assert.Equal("2002-03", rows[0][0]?.ToString());
        Assert.Equal("+200.0%", rows[1][9]?.ToString());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/reports/Performance?groupBy=decade")).StatusCode);
    }

    [Fact]
    public async Task Users_can_update_only_their_own_contact_details()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "cat_profile", 7);
        var emp = await app.LoginAsync("cat_profile", TestApp.EmployeePassword);

        var me = await (await emp.PutAsJsonAsync("/api/auth/me", new { email = "profile@example.com", phone = "0551234567", fullName = "اسم مزيف", role = "SuperAdmin" }))
            .ReadAsync<UserInfoDto>();
        Assert.Equal("profile@example.com", me.Email);
        Assert.Equal("0551234567", me.Phone);
        Assert.Equal("Employee", me.Role);
        Assert.Equal($"موظف cat_profile", me.FullName);

        var detail = await (await admin.GetAsync($"/api/employees/{e.Id}")).ReadAsync<EmployeeDetailDto>();
        Assert.Equal("0551234567", detail.Phone);
        Assert.Equal(7m, detail.DeductionPercentage);

        // Email uniqueness is enforced across accounts.
        await app.CreateEmployeeAsync(admin, "cat_profile2", 0);
        var other = await app.LoginAsync("cat_profile2", TestApp.EmployeePassword);
        Assert.Equal(HttpStatusCode.Conflict, (await other.PutAsJsonAsync("/api/auth/me", new { email = "profile@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PutAsJsonAsync("/api/auth/me", new { phone = "abc<script>" })).StatusCode);
    }
}
