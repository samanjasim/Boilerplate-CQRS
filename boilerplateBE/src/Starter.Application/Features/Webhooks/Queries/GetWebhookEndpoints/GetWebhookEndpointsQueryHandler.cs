using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Webhooks.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Queries.GetWebhookEndpoints;

internal sealed class GetWebhookEndpointsQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetWebhookEndpointsQuery, Result<List<WebhookEndpointDto>>>
{
    public async Task<Result<List<WebhookEndpointDto>>> Handle(
        GetWebhookEndpointsQuery request, CancellationToken cancellationToken)
    {
        var endpoints = await context.WebhookEndpoints
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var endpointIds = endpoints.Select(e => e.Id).ToList();

        var lastDeliveries = await context.WebhookDeliveries
            .AsNoTracking()
            .Where(d => endpointIds.Contains(d.WebhookEndpointId))
            .GroupBy(d => d.WebhookEndpointId)
            .Select(g => new
            {
                EndpointId = g.Key,
                Status = g.OrderByDescending(d => d.CreatedAt).First().Status,
                CreatedAt = g.OrderByDescending(d => d.CreatedAt).First().CreatedAt
            })
            .ToDictionaryAsync(x => x.EndpointId, x => x, cancellationToken);

        var dtos = endpoints.Select(e =>
        {
            lastDeliveries.TryGetValue(e.Id, out var last);
            return e.ToDto(
                last?.Status.ToString(),
                last?.CreatedAt);
        }).ToList();

        return Result.Success(dtos);
    }
}
