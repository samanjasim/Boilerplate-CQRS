using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Webhooks.Domain.Errors;
using Starter.Module.Webhooks.Application.DTOs;
using Starter.Module.Webhooks.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Queries.GetWebhookEndpointById;

internal sealed class GetWebhookEndpointByIdQueryHandler(
    WebhooksDbContext context) : IRequestHandler<GetWebhookEndpointByIdQuery, Result<WebhookEndpointDto>>
{
    public async Task<Result<WebhookEndpointDto>> Handle(
        GetWebhookEndpointByIdQuery request, CancellationToken cancellationToken)
    {
        var endpoint = await context.WebhookEndpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (endpoint is null)
            return Result.Failure<WebhookEndpointDto>(WebhookErrors.EndpointNotFound);

        var lastDelivery = await context.WebhookDeliveries
            .AsNoTracking()
            .Where(d => d.WebhookEndpointId == endpoint.Id)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.Status, d.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(endpoint.ToDto(
            lastDelivery?.Status.ToString(),
            lastDelivery?.CreatedAt));
    }
}
