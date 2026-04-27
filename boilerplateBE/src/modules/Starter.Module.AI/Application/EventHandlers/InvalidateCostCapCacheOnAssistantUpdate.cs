using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Costs;
using Starter.Module.AI.Domain.Events;

namespace Starter.Module.AI.Application.EventHandlers;

/// <summary>
/// Invalidates the cost-cap resolver's cached entry for `(TenantId, AssistantId)` whenever
/// the assistant's budget or other cap-relevant fields change.
///
/// Tenant plan changes are not subscribed here (they are module-internal to Billing); the
/// resolver's 60s TTL refreshes naturally — acceptable for a setting whose change rate is
/// minutes, not seconds.
/// </summary>
internal sealed class InvalidateCostCapCacheOnAssistantUpdate(
    ICostCapResolver resolver,
    ILogger<InvalidateCostCapCacheOnAssistantUpdate> logger) : INotificationHandler<AssistantUpdatedEvent>
{
    public async Task Handle(AssistantUpdatedEvent notification, CancellationToken cancellationToken)
    {
        await resolver.InvalidateAsync(notification.TenantId, notification.AssistantId, cancellationToken);
        logger.LogDebug(
            "Invalidated cost-cap cache for tenant {TenantId} assistant {AssistantId} after AssistantUpdatedEvent.",
            notification.TenantId, notification.AssistantId);
    }
}
