using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Default <see cref="IQuotaChecker"/> registered when no plan-aware module
/// (e.g. Billing) is installed. Treats every tenant as having unlimited quota.
/// Increment is a no-op.
/// </summary>
public sealed class NullQuotaChecker : IQuotaChecker
{
    public Task<QuotaResult> CheckAsync(
        Guid tenantId,
        string metric,
        int increment = 1,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(QuotaResult.Unlimited());

    public Task IncrementAsync(
        Guid tenantId,
        string metric,
        int amount = 1,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
