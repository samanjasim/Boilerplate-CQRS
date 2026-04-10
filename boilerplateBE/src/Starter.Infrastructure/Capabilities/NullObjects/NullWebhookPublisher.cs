using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="IWebhookPublisher"/> registered when the
/// Webhooks module is not installed. Publishing is a silent no-op so callers
/// (event handlers, command handlers) need no module-awareness.
/// </summary>
public sealed class NullWebhookPublisher(ILogger<NullWebhookPublisher> logger) : IWebhookPublisher
{
    public Task PublishAsync(
        string eventType,
        Guid? tenantId,
        object data,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Webhook publish skipped — Webhooks module not installed (event: {EventType}, tenant: {TenantId})",
            eventType, tenantId);
        return Task.CompletedTask;
    }
}
