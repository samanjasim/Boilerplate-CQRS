using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="IMessageDispatcher"/> registered when the
/// Communication module is not installed. Dispatching is a silent no-op so
/// callers (event handlers, command handlers) need no module-awareness.
/// </summary>
public sealed class NullMessageDispatcher(ILogger<NullMessageDispatcher> logger) : IMessageDispatcher
{
    public Task<Guid> SendAsync(
        string templateName,
        Guid recipientUserId,
        Dictionary<string, object> variables,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Message dispatch skipped — Communication module not installed (template: {TemplateName}, recipient: {RecipientUserId})",
            templateName, recipientUserId);
        return Task.FromResult(Guid.Empty);
    }

    public Task<Guid> SendToChannelAsync(
        string templateName,
        Guid recipientUserId,
        NotificationChannelType channel,
        Dictionary<string, object> variables,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Message dispatch skipped — Communication module not installed (template: {TemplateName}, channel: {Channel}, recipient: {RecipientUserId})",
            templateName, channel, recipientUserId);
        return Task.FromResult(Guid.Empty);
    }
}
