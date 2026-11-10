namespace SalesSystem.Api.Infrastructure;

public class AppOptions
{
    public const string Section = "App";
    /// <summary>Business time zone used for "today", daily grouping and date filters.</summary>
    public string TimeZone { get; set; } = "Asia/Riyadh";
    public string Currency { get; set; } = "ر.س";
    public string CompanyName { get; set; } = "نظام إدارة المبيعات";
    public string[] CorsOrigins { get; set; } = ["http://localhost:5173"];
    /// <summary>Address of the only reverse proxy allowed to supply forwarded client IP and scheme headers.</summary>
    public string? TrustedProxyIp { get; set; }
}

public class StorageOptions
{
    public const string Section = "Storage";
    public string UploadsPath { get; set; } = "App_Data/uploads";
    public long MaxFileBytes { get; set; } = 5 * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } = [".pdf", ".jpg", ".jpeg", ".png", ".webp"];
}

public class DatabaseOptions
{
    public const string Section = "Database";
    /// <summary>PostgreSql (default; migrations in Data/Migrations) or Sqlite (tests and quick trials; Data/SqliteMigrations).</summary>
    public string Provider { get; set; } = "PostgreSql";
    public bool ApplyMigrationsOnStartup { get; set; } = true;
}

public class SeedOptions
{
    public const string Section = "Seed";
    public string AdminUsername { get; set; } = "admin";
    public string AdminFullName { get; set; } = "المدير العام";
    /// <summary>Initial admin password; supply via user-secrets/env. If empty in Development a random one is generated and logged once.</summary>
    public string? AdminPassword { get; set; }
}
