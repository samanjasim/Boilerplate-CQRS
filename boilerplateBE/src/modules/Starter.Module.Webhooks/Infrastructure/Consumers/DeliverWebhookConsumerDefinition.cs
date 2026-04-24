using MassTransit;

namespace Starter.Module.Webhooks.Infrastructure.Consumers;

/// <summary>
/// MassTransit definition for <see cref="DeliverWebhookConsumer"/>.
///
/// Applies an exponential backoff retry policy so that transient failures
/// (database unreachable, transient broker errors, dependency-resolution
/// hiccups) do not lose the delivery message. HTTP-level failures from the
/// customer's endpoint are already recorded per-endpoint inside the consumer
/// and do not propagate — those are handled by the DB-persisted
/// <c>WebhookDelivery</c> row, not by MassTransit retry.
/// </summary>
public sealed class DeliverWebhookConsumerDefinition : ConsumerDefinition<DeliverWebhookConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<DeliverWebhookConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Exponential(
            retryLimit: 5,
            minInterval: TimeSpan.FromSeconds(10),
            maxInterval: TimeSpan.FromMinutes(5),
            intervalDelta: TimeSpan.FromSeconds(30)));
    }
}
