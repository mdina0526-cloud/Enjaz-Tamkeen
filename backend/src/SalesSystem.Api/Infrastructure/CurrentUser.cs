using System.Security.Claims;
using SalesSystem.Api.Domain;

namespace SalesSystem.Api.Infrastructure;

public static class AppClaims
{
    public const string EmployeeId = "employee_id";
    public const string SecurityStamp = "security_stamp";
}

public interface ICurrentUser
{
    int UserId { get; }
    string Username { get; }
    bool IsAdmin { get; }
    /// <summary>Employee id bound to the token. Never taken from request bodies or query strings.</summary>
    int? EmployeeId { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }
}

public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int UserId => int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw new AppException(StatusCodes.Status401Unauthorized, "يجب تسجيل الدخول");

    public string Username => Principal?.FindFirstValue(ClaimTypes.Name) ?? "";

    public bool IsAdmin => Principal?.IsInRole(RoleNames.SuperAdmin) == true || Principal?.IsInRole(RoleNames.OperationsManager) == true;

    public int? EmployeeId => int.TryParse(Principal?.FindFirstValue(AppClaims.EmployeeId), out var id) ? id : null;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
