using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Starter.Application.Common.Behaviors;

public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const int SlowRequestThresholdMs = 500;

    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly Stopwatch _timer;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
        _timer = new Stopwatch();
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _timer.Start();

        var response = await next();

        _timer.Stop();

        var elapsedMilliseconds = _timer.ElapsedMilliseconds;

        if (elapsedMilliseconds > SlowRequestThresholdMs)
        {
            var requestName = typeof(TRequest).Name;

            // Do NOT destructure the request ({@Request}) — auth commands
            // (Login, Register, Reset/ChangePassword, Verify2FA, etc.) carry
            // plaintext secrets that would otherwise land in the log sinks.
            _logger.LogWarning(
                "Long running request: {RequestName} ({ElapsedMilliseconds}ms)",
                requestName,
                elapsedMilliseconds);
        }

        return response;
    }
}
