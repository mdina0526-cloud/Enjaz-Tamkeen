using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SalesSystem.Api.Infrastructure;

/// <summary>Expected business-rule failure; mapped to a ProblemDetails response with an Arabic message.</summary>
public class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static AppException NotFound(string message = "العنصر المطلوب غير موجود") => new(StatusCodes.Status404NotFound, message);
    public static AppException BadRequest(string message) => new(StatusCodes.Status400BadRequest, message);
    public static AppException Conflict(string message) => new(StatusCodes.Status409Conflict, message);
    public static AppException Forbidden(string message = "ليست لديك صلاحية لتنفيذ هذا الإجراء") => new(StatusCodes.Status403Forbidden, message);
}

public class AppExceptionHandler(ILogger<AppExceptionHandler> logger, IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int status;
        string detail;
        if (exception is AppException app)
        {
            status = app.StatusCode;
            detail = app.Message;
        }
        else
        {
            logger.LogError(exception, "Unhandled exception");
            status = StatusCodes.Status500InternalServerError;
            detail = "حدث خطأ غير متوقع في الخادم";
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = detail, Detail = detail },
        });
    }
}
