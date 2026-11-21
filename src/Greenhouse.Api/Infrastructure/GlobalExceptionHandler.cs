using Greenhouse.Core.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Greenhouse.Api.Infrastructure;

/// <summary>
/// Єдине місце обробки винятків: помилки клієнта стають 4xx ProblemDetails,
/// усе інше логується як помилка і повертає 500 без деталей реалізації. Сервер не падає.
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int status;
        string title;
        string? detail;
        switch (exception)
        {
            case ClientDataException client:
                status = client.Kind switch
                {
                    ErrorKind.NotFound => StatusCodes.Status404NotFound,
                    ErrorKind.Conflict => StatusCodes.Status409Conflict,
                    ErrorKind.Implausible => StatusCodes.Status422UnprocessableEntity,
                    _ => StatusCodes.Status400BadRequest,
                };
                title = "Некоректні дані";
                detail = client.Message;
                break;
            case BadHttpRequestException bad:
                status = bad.StatusCode;
                title = "Некоректний запит";
                detail = "Тіло запиту не вдалося прочитати: перевірте JSON і типи полів.";
                LogBadRequest(logger, bad.Message);
                break;
            default:
                status = StatusCodes.Status500InternalServerError;
                title = "Внутрішня помилка сервера";
                detail = null;
                LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
                break;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Некоректний запит: {Reason}")]
    private static partial void LogBadRequest(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Необроблений виняток у {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);
}
