using MassTransit;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Services;

public sealed class MassTransitMessagePublisher(IBus bus) : IMessagePublisher
{
    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        await bus.Publish(message, cancellationToken);
    }
}
