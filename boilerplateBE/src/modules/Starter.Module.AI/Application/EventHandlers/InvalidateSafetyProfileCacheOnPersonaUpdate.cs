using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Events;

namespace Starter.Module.AI.Application.EventHandlers;

/// <summary>
/// Flushes the safety-profile resolver's cache for the persona's tenant scope whenever an
/// <see cref="AiPersona"/> is updated. Personas drive the resolver's preset selection when an
/// assistant has no <c>SafetyPresetOverride</c>; the cache key isn't keyed by persona ID, so
/// invalidating the whole tenant prefix is the right grain.
/// </summary>
internal sealed class InvalidateSafetyProfileCacheOnPersonaUpdate(
    ISafetyProfileResolver resolver,
    ILogger<InvalidateSafetyProfileCacheOnPersonaUpdate> logger)
    : INotificationHandler<PersonaUpdatedEvent>
{
    public async Task Handle(PersonaUpdatedEvent notification, CancellationToken cancellationToken)
    {
        await resolver.InvalidateAsync(notification.TenantId, cancellationToken);
        logger.LogDebug(
            "Invalidated safety-profile cache for tenant {TenantId} after PersonaUpdatedEvent (persona {PersonaId}).",
            notification.TenantId, notification.PersonaId);
    }
}
