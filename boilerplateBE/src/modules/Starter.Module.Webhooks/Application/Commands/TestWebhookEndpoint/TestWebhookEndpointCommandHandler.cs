using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Webhooks.Domain.Errors;
using Starter.Module.Webhooks.Application.Messages;
using Starter.Module.Webhooks.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Commands.TestWebhookEndpoint;

public sealed class TestWebhookEndpointCommandHandler(
    WebhooksDbContext dbContext,
    ICurrentUserService currentUser,
    IMessagePublisher messagePublisher)
    : IRequestHandler<TestWebhookEndpointCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(
        TestWebhookEndpointCommand request,
        CancellationToken cancellationToken)
    {
        var endpoint = await dbContext.WebhookEndpoints
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (endpoint is null)
            return Result.Failure<Unit>(WebhookErrors.EndpointNotFound);

        if (!endpoint.IsActive)
            return Result.Failure<Unit>(WebhookErrors.EndpointNotActive);

        var payload = JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            type = "webhook.test",
            tenantId = currentUser.TenantId,
            timestamp = DateTime.UtcNow,
            data = new { message = "This is a test webhook delivery" }
        });

        await messagePublisher.PublishAsync(
            new DeliverWebhookMessage(
                TenantId: endpoint.TenantId,
                EventType: "webhook.test",
                Payload: payload,
                OccurredAt: DateTime.UtcNow),
            cancellationToken);

        return Result.Success(Unit.Value);
    }
}
