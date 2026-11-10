using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SalesSystem.Api.Dtos;
using SalesSystem.Api.Services;

namespace SalesSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public Task<LoginResponse> Login(LoginRequest req, CancellationToken ct) => auth.LoginAsync(req, ct);

    /// <summary>Public: the employee name for a personal login link.</summary>
    [HttpGet("portal/{slug}")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public Task<LoginPortalDto> Portal(string slug, CancellationToken ct) => auth.GetPortalAsync(slug, ct);

    [HttpGet("me")]
    [Authorize]
    public Task<UserInfoDto> Me(CancellationToken ct) => auth.GetMeAsync(ct);

    [HttpPut("me")]
    [Authorize]
    public Task<UserInfoDto> UpdateMe(UpdateProfileRequest req, CancellationToken ct) => auth.UpdateProfileAsync(req, ct);

    [HttpPost("mfa/setup")]
    [Authorize]
    public Task<MfaSetupDto> BeginMfa(BeginMfaRequest req, CancellationToken ct) => auth.BeginMfaAsync(req, ct);

    [HttpPost("mfa/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmMfa(ConfirmMfaRequest req, CancellationToken ct)
    {
        await auth.ConfirmMfaAsync(req, ct);
        return NoContent();
    }

    [HttpPost("mfa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableMfa(DisableMfaRequest req, CancellationToken ct)
    {
        await auth.DisableMfaAsync(req, ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(req, ct);
        return NoContent();
    }
}
