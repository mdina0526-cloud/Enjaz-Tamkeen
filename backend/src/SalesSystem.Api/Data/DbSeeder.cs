using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Infrastructure;
using SalesSystem.Api.Services;

namespace SalesSystem.Api.Data;

public class DbSeeder(AppDbContext db, PasswordService passwords, IOptions<SeedOptions> options, IHostEnvironment env, ILogger<DbSeeder> logger, BusinessClock clock)
{
    private readonly SeedOptions _opt = options.Value;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedAdminAsync(ct);
        await BackfillLoginLinksAsync(ct);
    }

    private async Task SeedAdminAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Role.Name == RoleNames.SuperAdmin, ct)) return;

        var password = _opt.AdminPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            if (!env.IsDevelopment())
            {
                logger.LogCritical("No SuperAdmin exists and Seed:AdminPassword is not configured. Set it via environment variable Seed__AdminPassword.");
                return;
            }
            password = GeneratePassword();
            logger.LogWarning("Seed:AdminPassword not set. Generated a one-time development password for '{User}': {Password}  (change it after first login)",
                _opt.AdminUsername, password);
        }

        var admin = new User
        {
            Username = _opt.AdminUsername.Trim().ToLowerInvariant(),
            FullName = _opt.AdminFullName,
            RoleId = await db.Roles.Where(r => r.Name == RoleNames.SuperAdmin).Select(r => r.Id).FirstAsync(ct),
            CreatedAtUtc = clock.UtcNow,
        };
        admin.PasswordHash = passwords.Hash(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("SuperAdmin '{User}' created", admin.Username);
    }

    /// <summary>Employees created before personal login links existed get one on startup.</summary>
    private async Task BackfillLoginLinksAsync(CancellationToken ct)
    {
        var missing = await db.Employees.Where(e => e.LoginSlug == null).ToListAsync(ct);
        if (missing.Count == 0) return;
        foreach (var e in missing) e.LoginSlug = LoginLinks.NewSlug();
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Issued personal login links for {Count} employees", missing.Count);
    }

    private static string GeneratePassword() =>
        "Dev-" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(9)).Replace('+', 'x').Replace('/', 'y') + "1a";
}
