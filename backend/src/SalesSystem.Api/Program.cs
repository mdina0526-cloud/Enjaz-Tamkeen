using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Infrastructure;
using SalesSystem.Api.Services;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
// Bundled Arabic font (OFL) so PDF exports render identically on any server.
foreach (var font in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts"), "*.ttf"))
    QuestPDF.Drawing.FontManager.RegisterFontFromStream(File.OpenRead(font));

var createSuperAdmin = args.Contains("--create-superadmin", StringComparer.Ordinal);
var resetSuperAdmin = args.Contains("--reset-superadmin", StringComparer.Ordinal);
if (createSuperAdmin && resetSuperAdmin)
    throw new InvalidOperationException("Choose either --create-superadmin or --reset-superadmin.");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--create-superadmin" && arg != "--reset-superadmin").ToArray());
var config = builder.Configuration;
var newAdminUsername = Environment.GetEnvironmentVariable("NEW_SUPERADMIN_USERNAME")?.Trim().ToLowerInvariant();
var newAdminPassword = Environment.GetEnvironmentVariable("NEW_SUPERADMIN_PASSWORD");
var newAdminFullName = Environment.GetEnvironmentVariable("NEW_SUPERADMIN_FULL_NAME")?.Trim();
var resetAdminUsername = Environment.GetEnvironmentVariable("RESET_SUPERADMIN_USERNAME")?.Trim().ToLowerInvariant();
var resetAdminPassword = Environment.GetEnvironmentVariable("RESET_SUPERADMIN_PASSWORD");
if (createSuperAdmin &&
    (string.IsNullOrWhiteSpace(newAdminUsername) ||
     !Regex.IsMatch(newAdminUsername, "^[a-z0-9._-]{3,64}$") ||
     string.IsNullOrWhiteSpace(newAdminFullName) || newAdminFullName.Length > 150 ||
     string.IsNullOrWhiteSpace(newAdminPassword) || newAdminPassword.Length > 128))
    throw new InvalidOperationException("Set NEW_SUPERADMIN_USERNAME (3-64 English letters, digits, ._-), NEW_SUPERADMIN_FULL_NAME (1-150 characters), and NEW_SUPERADMIN_PASSWORD (8-128 characters).");
if (createSuperAdmin) PasswordService.EnsureStrong(newAdminPassword!);
if (resetSuperAdmin &&
    (string.IsNullOrWhiteSpace(resetAdminUsername) ||
     string.IsNullOrWhiteSpace(resetAdminPassword) || resetAdminPassword.Length > 128))
    throw new InvalidOperationException("Set RESET_SUPERADMIN_USERNAME and RESET_SUPERADMIN_PASSWORD (1-128 characters).");

// ---------- Structured logging: JSON lines outside development, with scopes (request id, user) on every entry ----------
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
    builder.Logging.AddSimpleConsole(o => { o.IncludeScopes = true; o.TimestampFormat = "HH:mm:ss "; });
else
    builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ "; o.UseUtcTimestamp = true; });

builder.Services.Configure<AppOptions>(config.GetSection(AppOptions.Section));
builder.Services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));
builder.Services.Configure<SeedOptions>(config.GetSection(SeedOptions.Section));
var dbOptions = config.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new DatabaseOptions();
if (resetSuperAdmin) PasswordService.EnsureStrong(resetAdminPassword!);
var appOptions = config.GetSection(AppOptions.Section).Get<AppOptions>() ?? new AppOptions();
if (!string.IsNullOrWhiteSpace(appOptions.TrustedProxyIp))
{
    if (!IPAddress.TryParse(appOptions.TrustedProxyIp, out var trustedProxy))
        throw new InvalidOperationException("App:TrustedProxyIp must be a valid IP address.");
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownProxies.Add(trustedProxy);
    });
}

// ---------- JWT signing key: never in source control ----------
var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
        throw new InvalidOperationException("Jwt:Key is missing or shorter than 32 characters. Set it via the Jwt__Key environment variable.");
    jwt.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    Console.WriteLine("WARNING: Jwt:Key not configured - using an ephemeral development key (tokens are invalidated on restart).");
}
builder.Services.Configure<JwtOptions>(o =>
{
    o.Issuer = jwt.Issuer;
    o.Audience = jwt.Audience;
    o.Key = jwt.Key;
    o.ExpiryMinutes = jwt.ExpiryMinutes;
});

// ---------- Database ----------
var connectionString = config.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default is missing.");
if (dbOptions.Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddDbContext<AppDbContext, SqliteAppDbContext>(o => o.UseSqlite(connectionString));
else
    builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

// ---------- Application services ----------
builder.Services.AddHttpContextAccessor();
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection().SetApplicationName("SalesSystem").PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BusinessClock>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<TotpService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddSingleton<ReportExporter>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<RentalService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddScoped<DailyCloseService>();

// ---------- Authentication & authorization ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = jwt.SigningKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role,
    };
    o.Events = new JwtBearerEvents
    {
        // Reject tokens of deactivated users or issued before a password reset / role change.
        OnTokenValidated = async ctx =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var userId = int.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
            var stamp = ctx.Principal?.FindFirstValue(AppClaims.SecurityStamp);
            var ok = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && u.SecurityStamp == stamp);
            if (!ok) ctx.Fail("Session is no longer valid");
        },
    };
});
builder.Services.AddAuthorization(o =>
{
    // Secure by default: every endpoint requires a signed-in user unless marked [AllowAnonymous].
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("Security:LoginAttemptsPerMinute", 10), Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(appOptions.CorsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Sales Management API", Version = "v1", Description = "نظام إدارة المبيعات والموظفين والعمليات المالية" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT from POST /api/auth/login",
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", doc)] = [] });
});

var app = builder.Build();

app.UseExceptionHandler();
if (!string.IsNullOrWhiteSpace(appOptions.TrustedProxyIp)) app.UseForwardedHeaders();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers.XFrameOptions = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.DocumentTitle = "Sales Management API");
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

// ---------- Database bootstrap ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbOptions.ApplyMigrationsOnStartup) await db.Database.MigrateAsync();
    if (createSuperAdmin)
    {
        if (await db.Users.AnyAsync(u => u.Username == newAdminUsername || u.Email == newAdminUsername))
            throw new InvalidOperationException("The requested username is already in use.");

        var roleId = await db.Roles.Where(r => r.Name == RoleNames.SuperAdmin).Select(r => r.Id).SingleAsync();
        var user = new User
        {
            Username = newAdminUsername!,
            FullName = newAdminFullName!,
            RoleId = roleId,
        };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<PasswordService>().Hash(user, newAdminPassword!);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        Console.WriteLine($"SuperAdmin '{user.Username}' created successfully.");
        return;
    }
    if (resetSuperAdmin)
    {
        var user = await db.Users.Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.Username == resetAdminUsername && u.Role.Name == RoleNames.SuperAdmin)
            ?? throw new InvalidOperationException("The requested SuperAdmin account was not found.");
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<PasswordService>().Hash(user, resetAdminPassword!);
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.MfaEnabled = false;
        user.MfaSecretProtected = null;
        user.MfaLastUsedStep = null;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        Console.WriteLine($"Password reset and lockout cleared for SuperAdmin '{user.Username}'.");
        return;
    }
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

app.Run();

public partial class Program;
