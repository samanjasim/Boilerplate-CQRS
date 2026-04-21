using MassTransit;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Services;

public sealed class MassTransitMessagePublisher(IPublishEndpoint publishEndpoint) : IMessagePublisher
{
    // Scoped IPublishEndpoint routes through the EF outbox when a registered DbContext
    // is tracked in the current scope; bare IBus.Publish would bypass both outboxes.
    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        await publishEndpoint.Publish(message, cancellationToken);
    }
}
