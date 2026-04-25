using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Webhooks.Application.Messages;
using Starter.Module.Webhooks.Domain.Enums;
using Starter.Module.Webhooks.Domain.Errors;
using Starter.Module.Webhooks.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Commands.RedeliverWebhook;

public sealed class RedeliverWebhookCommandHandler(
    WebhooksDbContext dbContext,
    IBus bus)
    : IRequestHandler<RedeliverWebhookCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(
        RedeliverWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var delivery = await dbContext.WebhookDeliveries
            .Include(d => d.Endpoint)
            .FirstOrDefaultAsync(d => d.Id == request.DeliveryId, cancellationToken);

        if (delivery is null)
            return Result.Failure<Unit>(WebhookErrors.DeliveryNotFound);

        // Only failed deliveries can be redelivered. Pending deliveries are still in flight;
        // successful ones don't need replay.
        if (delivery.Status != WebhookDeliveryStatus.Failed)
            return Result.Failure<Unit>(WebhookErrors.DeliveryNotRedeliverable);

        if (!delivery.Endpoint.IsActive)
            return Result.Failure<Unit>(WebhookErrors.EndpointNotActive);

        // Publish directly via IBus rather than IMessagePublisher (which wraps
        // IPublishEndpoint). The Webhooks module has no AddEntityFrameworkOutbox
        // registration, so IPublishEndpoint routes through whichever outbox was
        // registered last (WorkflowDbContext). That outbox is never saved by this
        // handler → the message would silently vanish (the documented dual-outbox
        // bug, see IIntegrationEventCollector).
        //
        // Bypassing the outbox is acceptable here: the user is actively waiting on
        // the response, and we are not committing business data alongside the
        // publish — so the at-least-once guarantee the outbox provides is irrelevant.
        // If the broker is unreachable, the request fails and the user retries.
        await bus.Publish(
            new DeliverWebhookMessage(
                TenantId: delivery.TenantId,
                EventType: delivery.EventType,
                Payload: delivery.RequestPayload,
                OccurredAt: DateTime.UtcNow),
            cancellationToken);

        return Result.Success(Unit.Value);
    }
}
