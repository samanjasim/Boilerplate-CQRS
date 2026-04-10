namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Synchronous quota check capability used by core and modules to gate
/// resource-creating actions (e.g. "can this tenant create another order?").
///
/// Implementations:
/// <list type="bullet">
///   <item><c>NullQuotaChecker</c> (core, default) — always returns Unlimited</item>
///   <item><c>FeatureFlagQuotaChecker</c> (core, opt-in) — reads limits from feature flags</item>
///   <item><c>PlanQuotaChecker</c> (Billing module) — reads limits from the tenant's active plan</item>
/// </list>
///
/// Whichever implementation is registered last wins. Modules override the
/// default by calling <c>services.AddScoped&lt;IQuotaChecker, ...&gt;()</c>
/// in their <c>ConfigureServices</c>.
/// </summary>
public interface IQuotaChecker : ICapability
{
    /// <summary>
    /// Check whether <paramref name="tenantId"/> can perform <paramref name="increment"/>
    /// more units of <paramref name="metric"/> without exceeding its quota.
    /// </summary>
    Task<QuotaResult> CheckAsync(
        Guid tenantId,
        string metric,
        int increment = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increment usage tracking for the given metric. Should be called after a
    /// successful check + write, ideally inside the same transaction.
    /// </summary>
    Task IncrementAsync(
        Guid tenantId,
        string metric,
        int amount = 1,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a quota check. <c>Allowed</c> indicates whether the action may
/// proceed; <c>Reason</c> is a human-readable explanation when it may not.
/// </summary>
public sealed record QuotaResult(bool Allowed, long Current, long Limit, string? Reason)
{
    public static QuotaResult Unlimited() => new(true, 0, long.MaxValue, null);

    public static QuotaResult Exceeded(long current, long limit) =>
        new(false, current, limit, $"Quota exceeded: {current}/{limit}");
}
