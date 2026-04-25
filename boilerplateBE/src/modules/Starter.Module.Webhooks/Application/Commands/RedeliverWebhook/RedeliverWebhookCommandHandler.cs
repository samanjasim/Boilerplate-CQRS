using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Webhooks.Application.Messages;
using Starter.Module.Webhooks.Domain.Enums;
using Starter.Module.Webhooks.Domain.Errors;
using Starter.Module.Webhooks.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Commands.RedeliverWebhook;

public sealed class RedeliverWebhookCommandHandler(
    WebhooksDbContext dbContext,
    IApplicationDbContext appContext,
    IMessagePublisher messagePublisher)
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

        // Only failed deliveries can be redelivered. Pending deliveries are still
        // in flight; successful ones don't need replay.
        if (delivery.Status != WebhookDeliveryStatus.Failed)
            return Result.Failure<Unit>(WebhookErrors.DeliveryNotRedeliverable);

        if (!delivery.Endpoint.IsActive)
            return Result.Failure<Unit>(WebhookErrors.EndpointNotActive);

        await messagePublisher.PublishAsync(
            new DeliverWebhookMessage(
                TenantId: delivery.TenantId,
                EventType: delivery.EventType,
                Payload: delivery.RequestPayload,
                OccurredAt: DateTime.UtcNow),
            cancellationToken);

        // Flush appContext to commit the outbox row scheduled by PublishAsync.
        // No domain writes here — the SaveChangesAsync just runs the
        // IntegrationEventOutboxInterceptor.
        await appContext.SaveChangesAsync(cancellationToken);

        return Result.Success(Unit.Value);
    }
}
