namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Dispatches messages to users via configured notification channels.
/// Implemented by the Communication module; when the module is not installed,
/// a silent no-op Null Object is registered so callers need not check for
/// installation.
/// </summary>
public interface IMessageDispatcher : ICapability
{
    /// <summary>
    /// Send a transactional message to a specific user using the tenant's default
    /// channel for the template, with automatic fallback per trigger rule config.
    /// </summary>
    Task<Guid> SendAsync(
        string templateName,
        Guid recipientUserId,
        Dictionary<string, object> variables,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a transactional message to a specific user via an explicit channel.
    /// Falls back to InApp if the channel is not configured.
    /// </summary>
    Task<Guid> SendToChannelAsync(
        string templateName,
        Guid recipientUserId,
        NotificationChannelType channel,
        Dictionary<string, object> variables,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}
