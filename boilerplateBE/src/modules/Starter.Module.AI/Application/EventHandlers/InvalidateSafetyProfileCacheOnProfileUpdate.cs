using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Events;

namespace Starter.Module.AI.Application.EventHandlers;

/// <summary>
/// Flushes the safety-profile resolver's cache for the affected tenant scope whenever an
/// <see cref="AiSafetyPresetProfile"/> row is created, updated, or deactivated. When the
/// updated row is a platform default (<c>TenantId == null</c>), the platform prefix is
/// flushed; tenants that have no override of their own will pick up the new platform
/// default on the next miss. Tenants <em>with</em> overrides aren't affected by platform
/// changes, so keying invalidation on the row's <c>TenantId</c> matches the resolver's
/// precedence rule exactly.
/// </summary>
internal sealed class InvalidateSafetyProfileCacheOnProfileUpdate(
    ISafetyProfileResolver resolver,
    ILogger<InvalidateSafetyProfileCacheOnProfileUpdate> logger)
    : INotificationHandler<SafetyPresetProfileUpdatedEvent>
{
    public async Task Handle(SafetyPresetProfileUpdatedEvent notification, CancellationToken cancellationToken)
    {
        await resolver.InvalidateAsync(notification.TenantId, cancellationToken);
        logger.LogDebug(
            "Invalidated safety-profile cache for tenant {TenantId} after SafetyPresetProfileUpdatedEvent (profile {ProfileId}).",
            notification.TenantId, notification.ProfileId);
    }
}
