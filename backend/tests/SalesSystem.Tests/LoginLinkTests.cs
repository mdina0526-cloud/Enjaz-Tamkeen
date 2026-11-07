using System.Net;
using System.Net.Http.Json;
using SalesSystem.Api.Dtos;

namespace SalesSystem.Tests;

public class LoginLinkTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task Each_employee_gets_a_unique_link_that_shows_only_their_name()
    {
        var admin = await app.AdminAsync();
        var a = await app.CreateEmployeeAsync(admin, "link_a", 0);
        var b = await app.CreateEmployeeAsync(admin, "link_b", 0);

        Assert.False(string.IsNullOrEmpty(a.LoginSlug));
        Assert.True(a.LoginSlug!.Length >= 22);
        Assert.NotEqual(a.LoginSlug, b.LoginSlug);

        var portal = await (await app.CreateClient().GetAsync($"/api/auth/portal/{a.LoginSlug}")).ReadAsync<LoginPortalDto>();
        Assert.Equal("موظف link_a", portal.FullName);
        Assert.Equal(HttpStatusCode.NotFound, (await app.CreateClient().GetAsync("/api/auth/portal/not-a-real-link-000000")).StatusCode);
    }

    [Fact]
    public async Task Only_the_owner_can_sign_in_through_their_link()
    {
        var admin = await app.AdminAsync();
        var a = await app.CreateEmployeeAsync(admin, "link_owner", 0);
        var client = app.CreateClient();

        var ok = await (await client.PostAsJsonAsync("/api/auth/login", new { portal = a.LoginSlug, password = TestApp.EmployeePassword })).ReadAsync<LoginResponse>();
        Assert.Equal("link_owner", ok.User.Username);

        // Another account's credentials (even the admin's) are rejected on this link.
        var other = await client.PostAsJsonAsync("/api/auth/login", new { portal = a.LoginSlug, username = "admin", password = TestApp.AdminPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { portal = a.LoginSlug, password = "Wrong1234" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new { password = "x" })).StatusCode);
    }

    [Fact]
    public async Task Regenerating_revokes_the_old_link_and_disabled_accounts_have_no_page()
    {
        var admin = await app.AdminAsync();
        var e = await app.CreateEmployeeAsync(admin, "link_regen", 0);
        var client = app.CreateClient();

        var updated = await (await admin.PostAsync($"/api/employees/{e.Id}/login-link", null)).ReadAsync<EmployeeDetailDto>();
        Assert.NotEqual(e.LoginSlug, updated.LoginSlug);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/auth/portal/{e.LoginSlug}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { portal = e.LoginSlug, password = TestApp.EmployeePassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { portal = updated.LoginSlug, password = TestApp.EmployeePassword })).StatusCode);

        (await admin.PostAsJsonAsync($"/api/employees/{e.Id}/status", new { isActive = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/auth/portal/{updated.LoginSlug}")).StatusCode);

        var emp = await app.CreateEmployeeAsync(admin, "link_noadmin", 0);
        var empClient = await app.LoginAsync("link_noadmin", TestApp.EmployeePassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await empClient.PostAsync($"/api/employees/{emp.Id}/login-link", null)).StatusCode);
    }
}
