namespace Starter.Module.AI.Application.Services.Costs;

public enum CapWindow
{
    Daily,
    Monthly
}

/// <summary>
/// Result of an atomic cap-claim attempt. `Granted=false` means the claim was refused
/// because it would exceed the cap; `CurrentUsd` and `CapUsd` carry the values
/// observed at the moment of refusal so callers can render a friendly message.
/// </summary>
public sealed record ClaimResult(bool Granted, decimal CurrentUsd, decimal CapUsd);

/// <summary>
/// Atomic check-and-increment accounting for AI cost caps in Redis. All operations are
/// keyed by `(TenantId, AssistantId, CapWindow)` and atomic via Lua scripts so concurrent
/// claims cannot exceed the cap. Truth lives in `AiUsageLog`; this service is the hot
/// path that gates each agent step.
/// </summary>
public interface ICostCapAccountant
{
    Task<ClaimResult> TryClaimAsync(
        Guid tenantId, Guid assistantId, decimal estimatedUsd,
        CapWindow window, decimal capUsd, CancellationToken ct = default);

    Task RollbackClaimAsync(
        Guid tenantId, Guid assistantId, decimal estimatedUsd,
        CapWindow window, CancellationToken ct = default);

    /// <summary>
    /// Reconciles a previously-claimed estimate against the actual consumption.
    /// `delta = actualUsd - estimatedUsd`; typically negative (estimate is upper-bound).
    /// </summary>
    Task RecordActualAsync(
        Guid tenantId, Guid assistantId, decimal deltaUsd,
        CapWindow window, CancellationToken ct = default);

    Task<decimal> GetCurrentAsync(
        Guid tenantId, Guid assistantId, CapWindow window, CancellationToken ct = default);
}
