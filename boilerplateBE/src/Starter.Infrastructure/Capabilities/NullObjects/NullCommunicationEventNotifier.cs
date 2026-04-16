using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="ICommunicationEventNotifier"/> registered
/// when the Communication module is not installed. Event notification is a
/// silent no-op so callers need no module-awareness.
/// </summary>
public sealed class NullCommunicationEventNotifier(ILogger<NullCommunicationEventNotifier> logger) : ICommunicationEventNotifier
{
    public Task NotifyAsync(
        string eventName,
        Guid tenantId,
        Guid? actorUserId,
        Dictionary<string, object> eventData,
        CancellationToken ct = default)
    {
        logger.LogDebug(
            "Communication event notification skipped — Communication module not installed (event: {EventName}, tenant: {TenantId})",
            eventName, tenantId);
        return Task.CompletedTask;
    }
}
