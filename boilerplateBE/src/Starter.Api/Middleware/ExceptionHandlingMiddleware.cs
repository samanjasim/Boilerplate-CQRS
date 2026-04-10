using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using Starter.Abstractions.Capabilities;
using Starter.Domain.Exceptions;
using Starter.Shared.Models;

namespace Starter.Api.Middleware;

/// <summary>
/// Global exception handling middleware.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            ValidationException validationEx => HandleValidationException(validationEx),
            BusinessRuleException businessEx => HandleBusinessRuleException(businessEx),
            DomainException domainEx => HandleDomainException(domainEx),
            CapabilityNotInstalledException capEx => HandleCapabilityNotInstalledException(capEx),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,
                ApiResponse.Fail("Unauthorized access.")),
            _ => HandleUnknownException(exception)
        };

        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        _logger.LogError(exception,
            "Exception occurred: {Message}. TraceId: {TraceId}",
            exception.Message,
            traceId);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(response, JsonOptions);
        await context.Response.WriteAsync(json);
    }

    private static (int StatusCode, ApiResponse Response) HandleValidationException(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return (StatusCodes.Status400BadRequest, new ApiResponse
        {
            Success = false,
            Message = "Validation failed.",
            ValidationErrors = errors
        });
    }

    private static (int StatusCode, ApiResponse Response) HandleDomainException(DomainException exception)
    {
        return (StatusCodes.Status400BadRequest, ApiResponse.Fail(exception.Message));
    }

    private static (int StatusCode, ApiResponse Response) HandleBusinessRuleException(BusinessRuleException exception)
    {
        return (StatusCodes.Status422UnprocessableEntity, ApiResponse.Fail(exception.Message));
    }

    private static (int StatusCode, ApiResponse Response) HandleCapabilityNotInstalledException(
        CapabilityNotInstalledException exception)
    {
        // 501 Not Implemented: the server understands the request but lacks the
        // capability to fulfil it because the providing module is not installed
        // in this build. Operators install the named module to enable it.
        return (StatusCodes.Status501NotImplemented, ApiResponse.Fail(exception.Message));
    }

    private (int StatusCode, ApiResponse Response) HandleUnknownException(Exception exception)
    {
        var message = _env.IsDevelopment()
            ? exception.Message
            : "An error occurred while processing your request.";

        return (StatusCodes.Status500InternalServerError, ApiResponse.Fail(message));
    }
}

/// <summary>
/// Extension methods for exception handling middleware.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
