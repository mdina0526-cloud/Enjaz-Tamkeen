using System.Net;
using System.Net.Http.Json;
using SalesSystem.Api.Dtos;

namespace SalesSystem.Tests;

public class SecurityTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task Login_with_wrong_password_is_rejected()
    {
        var res = await app.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_require_a_token()
    {
        var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/employees")).StatusCode);
    }

    [Fact]
    public async Task Employee_cannot_call_admin_endpoints()
    {
        var admin = await app.AdminAsync();
        await app.CreateEmployeeAsync(admin, "sec_emp1", 5);
        var emp = await app.LoginAsync("sec_emp1", TestApp.EmployeePassword);

        foreach (var url in new[] { "/api/employees", "/api/rentals", "/api/reports/Sales", "/api/audit-logs", "/api/settings/deduction", "/api/dashboard/admin" })
            Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync(url)).StatusCode);

        var created = await TestApp.CreateOrderAsync(emp, 100);
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.PostAsync($"/api/orders/{created.Id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.PostAsJsonAsync($"/api/orders/{created.Id}/cancel", new { reason = "x" })).StatusCode);
    }

    [Fact]
    public async Task Employee_only_sees_own_orders_even_when_tampering_with_ids()
    {
        var admin = await app.AdminAsync();
        var e1 = await app.CreateEmployeeAsync(admin, "sec_a", 0);
        var e2 = await app.CreateEmployeeAsync(admin, "sec_b", 0);
        var c1 = await app.LoginAsync("sec_a", TestApp.EmployeePassword);
        var c2 = await app.LoginAsync("sec_b", TestApp.EmployeePassword);

        var o1 = await TestApp.CreateOrderAsync(c1, 100);
        var o2 = await TestApp.CreateOrderAsync(c2, 200);

        // Direct access to another employee's order → 404 (no existence leak).
        Assert.Equal(HttpStatusCode.NotFound, (await c1.GetAsync($"/api/orders/{o2.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c1.GetAsync($"/api/orders/{o1.Id}")).StatusCode);

        // Filtering by the other employee's id still returns only own orders.
        var list = await (await c1.GetAsync($"/api/orders?employeeId={e2.Id}&pageSize=200")).ReadAsync<PagedResult<OrderListItemDto>>();
        Assert.All(list.Items, o => Assert.Equal(e1.Id, o.EmployeeId));
        Assert.DoesNotContain(list.Items, o => o.Id == o2.Id);
    }

    [Fact]
    public async Task Employee_cannot_create_order_for_another_employee()
    {
        var admin = await app.AdminAsync();
        await app.CreateEmployeeAsync(admin, "sec_c", 0);
        var victim = await app.CreateEmployeeAsync(admin, "sec_d", 0);
        var c = await app.LoginAsync("sec_c", TestApp.EmployeePassword);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => TestApp.CreateOrderAsync(c, 100, employeeId: victim.Id));
        Assert.StartsWith("403", ex.Message);

        var victimOrders = await (await admin.GetAsync($"/api/employees/{victim.Id}/orders")).ReadAsync<PagedResult<OrderListItemDto>>();
        Assert.Equal(0, victimOrders.Total);
    }

    [Fact]
    public async Task Deactivated_employee_cannot_log_in_and_existing_token_is_revoked()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "sec_off", 0);
        var c = await app.LoginAsync("sec_off", TestApp.EmployeePassword);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/auth/me")).StatusCode);

        (await admin.PostAsJsonAsync($"/api/employees/{e.Id}/status", new { isActive = false })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        var login = await app.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "sec_off", password = TestApp.EmployeePassword });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Password_reset_revokes_old_tokens_and_new_password_works()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "sec_reset", 0);
        var old = await app.LoginAsync("sec_reset", TestApp.EmployeePassword);

        (await admin.PostAsJsonAsync($"/api/employees/{e.Id}/reset-password", new { newPassword = "NewPass456" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await old.GetAsync("/api/auth/me")).StatusCode);
        var fresh = await app.LoginAsync("sec_reset", "NewPass456");
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Weak_password_and_invalid_input_are_rejected_by_the_server()
    {
        var admin = await app.AdminAsync();
        var weak = await admin.PostAsJsonAsync("/api/employees", new { fullName = "x", username = "weakpw", password = "abcdefgh" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var badPct = await admin.PostAsJsonAsync("/api/employees", new { fullName = "x", username = "badpct", password = "Valid1234", deductionPercentage = 150 });
        Assert.Equal(HttpStatusCode.BadRequest, badPct.StatusCode);

        await app.CreateEmployeeAsync(admin, "sec_val", 0);
        var emp = await app.LoginAsync("sec_val", TestApp.EmployeePassword);
        var negative = await emp.PostAsJsonAsync("/api/orders", new { items = new[] { new { serviceTypeId = 1, quantity = 1, unitPrice = -50 } }, paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        var empty = await emp.PostAsJsonAsync("/api/orders", new { items = Array.Empty<object>(), paymentMethod = "Cash" });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Repeated_failed_logins_lock_the_account()
    {
        var admin = await app.AdminAsync();
        await app.CreateEmployeeAsync(admin, "sec_lock", 0);
        var client = app.CreateClient();
        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/auth/login", new { username = "sec_lock", password = "bad-pass-1" });

        var res = await client.PostAsJsonAsync("/api/auth/login", new { username = "sec_lock", password = TestApp.EmployeePassword });
        Assert.Equal((HttpStatusCode)423, res.StatusCode);
    }
}
