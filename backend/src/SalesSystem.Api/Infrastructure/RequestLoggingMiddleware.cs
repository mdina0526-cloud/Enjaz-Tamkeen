using System.Diagnostics;
using System.Security.Claims;

namespace SalesSystem.Api.Infrastructure;

/// <summary>
/// One structured log entry per API request (method, path, status, duration, user). Runs after authentication so the
/// user id is known; query strings and bodies are never logged to keep credentials and personal data out of logs.
/// </summary>
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api"))
        {
            await next(ctx);
            return;
        }

        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        // Message-template scope: readable in the console and emitted as named fields by the JSON formatter.
        using var scope = logger.BeginScope("RequestId:{RequestId} UserId:{UserId}", ctx.TraceIdentifier, userId ?? "-");
        var sw = Stopwatch.StartNew();
        try
        {
            await next(ctx);
        }
        finally
        {
            sw.Stop();
            var status = ctx.Response.StatusCode;
            var level = status >= 500 ? LogLevel.Error : status is 401 or 403 or 429 ? LogLevel.Warning : LogLevel.Information;
            logger.Log(level, "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms",
                ctx.Request.Method, ctx.Request.Path.Value, status, Math.Round(sw.Elapsed.TotalMilliseconds, 1));
        }
    }
}
