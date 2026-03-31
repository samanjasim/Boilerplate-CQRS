using Starter.Application.Common.Interfaces;
using Starter.Domain.Billing.Enums;

namespace Starter.Infrastructure.Services;

internal sealed class MockBillingProvider : IBillingProvider
{
    public Task<CreateSubscriptionResult> CreateSubscriptionAsync(
        Guid tenantId,
        string planSlug,
        BillingInterval interval,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var periodEnd = interval == BillingInterval.Annual
            ? now.AddDays(365)
            : now.AddDays(30);

        var result = new CreateSubscriptionResult(
            ExternalSubscriptionId: $"mock_sub_{Guid.NewGuid():N}",
            ExternalCustomerId: $"mock_cust_{tenantId:N}",
            PeriodStart: now,
            PeriodEnd: periodEnd);

        return Task.FromResult(result);
    }

    public Task<ChangeSubscriptionResult> ChangeSubscriptionAsync(
        string externalSubscriptionId,
        string newPlanSlug,
        BillingInterval interval,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var newPeriodEnd = interval == BillingInterval.Annual
            ? now.AddDays(365)
            : now.AddDays(30);

        var result = new ChangeSubscriptionResult(
            NewPeriodStart: now,
            NewPeriodEnd: newPeriodEnd,
            ProratedAmount: 0m);

        return Task.FromResult(result);
    }

    public Task CancelSubscriptionAsync(string externalSubscriptionId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
