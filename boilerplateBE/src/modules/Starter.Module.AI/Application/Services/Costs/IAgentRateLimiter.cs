namespace Starter.Module.AI.Application.Services.Costs;

/// <summary>
/// Per-agent sliding-window rate limiter. Returns true if the call is permitted (and
/// records it); false if the agent has exceeded `rpm` requests in the trailing 60s.
/// </summary>
public interface IAgentRateLimiter
{
    Task<bool> TryAcquireAsync(Guid assistantId, int rpm, CancellationToken ct = default);
}
