using System.Net;
using System.Text.Json;

namespace RiskGuard.API.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (KeyNotFoundException exception)
        {
            await WriteProblemAsync(context, HttpStatusCode.NotFound, "Resource not found", exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            await WriteProblemAsync(context, HttpStatusCode.Forbidden, "Access denied", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "Request conflicts with current state", exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled request failure for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.InternalServerError,
                "Unexpected server error",
                "The request could not be completed. Use the trace identifier when contacting support.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, HttpStatusCode status, string title, string detail)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = $"https://httpstatuses.com/{(int)status}",
            title,
            status = (int)status,
            detail,
            traceId = context.TraceIdentifier
        }));
    }
}
