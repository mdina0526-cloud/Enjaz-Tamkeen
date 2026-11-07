using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Infrastructure;

namespace SalesSystem.Api.Services;

public class PasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(User user, string password, out bool needsRehash)
    {
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        needsRehash = result == PasswordVerificationResult.SuccessRehashNeeded;
        return result != PasswordVerificationResult.Failed;
    }

    public static void EnsureStrong(string password)
    {
        if (password.Length < 8 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw AppException.BadRequest("كلمة المرور يجب أن تكون 8 أحرف على الأقل وتحتوي على حروف وأرقام");
    }
}

public class AuthService(AppDbContext db, PasswordService passwords, TokenService tokens, AuditService audit, BusinessClock clock, ICurrentUser currentUser)
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private const string InvalidCredentials = "اسم المستخدم أو كلمة المرور غير صحيحة";
    private const string InvalidPortalCredentials = "كلمة المرور غير صحيحة";

    /// <summary>Name shown on an employee's personal login page. Unknown links and disabled accounts look the same (404).</summary>
    public async Task<LoginPortalDto> GetPortalAsync(string slug, CancellationToken ct)
    {
        var info = await db.Employees.AsNoTracking()
            .Where(e => e.LoginSlug == slug && e.User.IsActive)
            .Select(e => new LoginPortalDto(e.User.FullName, e.JobTitle))
            .FirstOrDefaultAsync(ct);
        return info ?? throw AppException.NotFound("رابط تسجيل الدخول غير صالح أو منتهي");
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var portal = string.IsNullOrWhiteSpace(req.Portal) ? null : req.Portal.Trim();
        var login = req.Username?.Trim().ToLowerInvariant() ?? "";
        if (portal is null && login.Length == 0) throw AppException.BadRequest("اسم المستخدم مطلوب");

        var users = db.Users.Include(u => u.Role).Include(u => u.Employee);
        // A personal link identifies exactly one employee; nobody else can sign in through it.
        var user = portal is not null
            ? await users.FirstOrDefaultAsync(u => u.Employee != null && u.Employee.LoginSlug == portal, ct)
            : await users.FirstOrDefaultAsync(u => u.Username == login || u.Email == login, ct);

        if (user is null)
            throw new AppException(StatusCodes.Status401Unauthorized, portal is null ? InvalidCredentials : "رابط تسجيل الدخول غير صالح أو منتهي");

        var now = clock.UtcNow;
        if (user.LockoutEndUtc > now)
            throw new AppException(StatusCodes.Status423Locked, "تم قفل الحساب مؤقتاً بسبب محاولات دخول فاشلة متكررة. حاول لاحقاً");

        if (!passwords.Verify(user, req.Password, out var needsRehash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEndUtc = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }
            audit.Add(AuditActions.LoginFailed, "User", user.Id, userId: user.Id, username: user.Username);
            await db.SaveChangesAsync(ct);
            throw new AppException(StatusCodes.Status401Unauthorized, portal is null ? InvalidCredentials : InvalidPortalCredentials);
        }

        if (!user.IsActive)
            throw new AppException(StatusCodes.Status403Forbidden, "هذا الحساب معطل. تواصل مع المدير");

        if (needsRehash) user.PasswordHash = passwords.Hash(user, req.Password);
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = now;
        audit.Add(AuditActions.Login, "User", user.Id, portal is null ? null : new { via = "personal-link" }, userId: user.Id, username: user.Username);
        await db.SaveChangesAsync(ct);

        var (token, expires) = tokens.CreateToken(user);
        return new LoginResponse(token, expires, ToInfo(user));
    }

    public async Task<UserInfoDto> GetMeAsync(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Role).Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct) ?? throw AppException.NotFound();
        return ToInfo(user);
    }

    /// <summary>Self-service edit of contact details only; everything else on the account is managed by the admin.</summary>
    public async Task<UserInfoDto> UpdateProfileAsync(UpdateProfileRequest req, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Role).Include(u => u.Employee).FirstAsync(u => u.Id == currentUser.UserId, ct);
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLowerInvariant();
        if (email is not null && await db.Users.AnyAsync(u => u.Email == email && u.Id != user.Id, ct))
            throw AppException.Conflict("البريد الإلكتروني مستخدم مسبقاً");

        var before = new { user.Email, Phone = user.Employee?.Phone };
        user.Email = email;
        if (user.Employee is not null) user.Employee.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        audit.Add(AuditActions.Update, "User", user.Id, new { before, after = new { user.Email, Phone = user.Employee?.Phone }, self = true });
        await db.SaveChangesAsync(ct);
        return ToInfo(user);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == currentUser.UserId, ct);
        if (!passwords.Verify(user, req.CurrentPassword, out _))
            throw AppException.BadRequest("كلمة المرور الحالية غير صحيحة");
        PasswordService.EnsureStrong(req.NewPassword);

        user.PasswordHash = passwords.Hash(user, req.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        audit.Add(AuditActions.ChangePassword, "User", user.Id);
        await db.SaveChangesAsync(ct);
    }

    private static UserInfoDto ToInfo(User u) => new(
        u.Id, u.Username, u.FullName, u.Email, u.Employee?.Phone, u.Role.Name, u.Employee?.Id, u.Employee?.EmployeeCode, u.Employee?.JobTitle, u.LastLoginAtUtc);
}
