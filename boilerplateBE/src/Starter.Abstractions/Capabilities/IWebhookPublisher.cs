namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Emits a webhook event. Implemented by the Webhooks module; when the module
/// is not installed, <see cref="Starter.Abstractions.Capabilities.ICapability"/>
/// guidance applies — a silent no-op Null Object is registered in core so
/// callers do not need to check for installation.
/// </summary>
public interface IWebhookPublisher : ICapability
{
    Task PublishAsync(string eventType, Guid? tenantId, object data, CancellationToken cancellationToken = default);
}
