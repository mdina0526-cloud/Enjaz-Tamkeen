using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using SalesSystem.Api.Dtos;

namespace SalesSystem.Tests;

/// <summary>Boots the real API against a private in-memory SQLite database.</summary>
public class TestApp : WebApplicationFactory<Program>
{
    public const string AdminPassword = "Admin12345!";
    public const string EmployeePassword = "Employee123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly string _dbName = $"test-{Guid.NewGuid():N}";
    private readonly SqliteConnection _keepAlive;
    public string UploadsPath { get; } = Path.Combine(Path.GetTempPath(), "sales-tests", Guid.NewGuid().ToString("N"));

    public TestApp()
    {
        // The in-memory database lives as long as one connection to it stays open.
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
    }

    private string ConnectionString => $"Data Source=file:{_dbName}?mode=memory&cache=shared";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Jwt:Key", "test-signing-key-that-is-long-enough-1234567890");
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Storage:UploadsPath", UploadsPath);
        builder.UseSetting("Security:LoginAttemptsPerMinute", "1000");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _keepAlive.Dispose();
        try { Directory.Delete(UploadsPath, true); } catch { /* best effort */ }
    }

    public async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    public Task<HttpClient> AdminAsync() => LoginAsync("admin", AdminPassword);

    public async Task<EmployeeDetailDto> CreateEmployeeAsync(HttpClient admin, string username, decimal pct)
    {
        var res = await admin.PostAsJsonAsync("/api/employees", new
        {
            fullName = $"موظف {username}",
            username,
            password = EmployeePassword,
            deductionPercentage = pct,
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<EmployeeDetailDto>(Json))!;
    }

    public static async Task<OrderDetailDto> CreateOrderAsync(HttpClient client, decimal amount, string method = "Cash", int? employeeId = null, string? key = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                items = new[] { new { serviceTypeId = 1, quantity = 1, unitPrice = amount } },
                paymentMethod = method,
                employeeId,
            }),
        };
        if (key is not null) req.Headers.Add("Idempotency-Key", key);
        var res = await client.SendAsync(req);
        if (!res.IsSuccessStatusCode) throw new HttpRequestException($"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<OrderDetailDto>(Json))!;
    }
}

public static class HttpExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode) throw new HttpRequestException($"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<T>(TestApp.Json))!;
    }
}
