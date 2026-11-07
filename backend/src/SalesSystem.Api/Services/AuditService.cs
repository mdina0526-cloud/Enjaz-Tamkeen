using System.Text.Json;
using SalesSystem.Api.Data;
using SalesSystem.Api.Domain;
using SalesSystem.Api.Infrastructure;

namespace SalesSystem.Api.Services;

public static class AuditActions
{
    public const string Login = "Login";
    public const string RegenerateLoginLink = "RegenerateLoginLink";
    public const string LoginFailed = "LoginFailed";
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Approve = "Approve";
    public const string Cancel = "Cancel";
    public const string Activate = "Activate";
    public const string Deactivate = "Deactivate";
    public const string ResetPassword = "ResetPassword";
    public const string ChangePassword = "ChangePassword";
    public const string ChangeDeduction = "ChangeDeduction";
    public const string Archive = "Archive";
    public const string Void = "Void";
    public const string Export = "Export";
}

/// <summary>
/// Adds audit entries to the current DbContext so they commit atomically with the change they describe.
/// </summary>
public class AuditService(AppDbContext db, ICurrentUser currentUser, BusinessClock clock)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public void Add(string action, string entityType, object? entityId, object? details = null, int? userId = null, string? username = null)
    {
        var details_ = details is null ? null : JsonSerializer.Serialize(details, JsonOpts);
        if (details_ is { Length: > 4000 }) details_ = details_[..4000];

        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? (currentUser.IsAuthenticated ? currentUser.UserId : null),
            Username = username ?? (currentUser.IsAuthenticated ? currentUser.Username : null),
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            Details = details_,
            IpAddress = currentUser.IpAddress,
            CreatedAtUtc = clock.UtcNow,
        });
    }
}
