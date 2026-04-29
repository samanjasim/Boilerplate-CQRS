using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Events;

namespace Starter.Module.AI.Application.EventHandlers;

/// <summary>
/// Flushes the safety-profile resolver's cache for the affected tenant whenever an
/// <see cref="AiAssistant"/> mutates a moderation-relevant field (notably
/// <c>SafetyPresetOverride</c>). The resolver caches by <c>(tenantId, preset, provider)</c>
/// and invalidates by tenant prefix, so a single tenant invalidation flushes every cached
/// preset×provider tuple in one call.
/// </summary>
internal sealed class InvalidateSafetyProfileCacheOnAssistantUpdate(
    ISafetyProfileResolver resolver,
    ILogger<InvalidateSafetyProfileCacheOnAssistantUpdate> logger)
    : INotificationHandler<AssistantUpdatedEvent>
{
    public async Task Handle(AssistantUpdatedEvent notification, CancellationToken cancellationToken)
    {
        await resolver.InvalidateAsync(notification.TenantId, cancellationToken);
        logger.LogDebug(
            "Invalidated safety-profile cache for tenant {TenantId} after AssistantUpdatedEvent.",
            notification.TenantId);
    }
}
