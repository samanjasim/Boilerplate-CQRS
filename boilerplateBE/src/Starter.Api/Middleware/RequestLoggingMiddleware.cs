using System.Diagnostics;

namespace Starter.Api.Middleware;

/// <summary>
/// Request logging middleware.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;

        _logger.LogInformation(
            "Request started: {Method} {Path} | RequestId: {RequestId}",
            context.Request.Method,
            context.Request.Path,
            requestId);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            _logger.LogInformation(
                "Request completed: {Method} {Path} | Status: {StatusCode} | Duration: {Duration}ms | RequestId: {RequestId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                requestId);
        }
    }
}

/// <summary>
/// Extension methods for request logging middleware.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestLoggingMiddleware>();
    }
}
