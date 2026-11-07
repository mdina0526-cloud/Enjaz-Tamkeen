using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SalesSystem.Api.Domain;

namespace SalesSystem.Api.Infrastructure;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "SalesSystem";
    public string Audience { get; set; } = "SalesSystem.Client";
    /// <summary>Signing key (min 32 chars). Supplied via user-secrets or the Jwt__Key environment variable, never committed.</summary>
    public string Key { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 480;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));
}

public class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JwtOptions _opt = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateToken(User user)
    {
        var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(_opt.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.Name),
            new(AppClaims.SecurityStamp, user.SecurityStamp),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        if (user.Employee is not null)
            claims.Add(new Claim(AppClaims.EmployeeId, user.Employee.Id.ToString()));

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _opt.Issuer,
            Audience = _opt.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            SigningCredentials = new SigningCredentials(_opt.SigningKey, SecurityAlgorithms.HmacSha256),
        });

        return (token, expires);
    }
}
