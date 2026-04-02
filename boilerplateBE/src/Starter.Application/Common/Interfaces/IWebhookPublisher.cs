namespace Starter.Application.Common.Interfaces;

public interface IWebhookPublisher
{
    Task PublishAsync(string eventType, Guid? tenantId, object data, CancellationToken cancellationToken = default);
}
