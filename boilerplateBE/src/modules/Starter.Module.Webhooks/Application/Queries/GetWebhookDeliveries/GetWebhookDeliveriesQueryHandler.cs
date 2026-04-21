using Starter.Abstractions.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Models;
using Starter.Module.Webhooks.Application.DTOs;
using Starter.Module.Webhooks.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Queries.GetWebhookDeliveries;

internal sealed class GetWebhookDeliveriesQueryHandler(
    WebhooksDbContext context) : IRequestHandler<GetWebhookDeliveriesQuery, Result<PaginatedList<WebhookDeliveryDto>>>
{
    public async Task<Result<PaginatedList<WebhookDeliveryDto>>> Handle(
        GetWebhookDeliveriesQuery request, CancellationToken cancellationToken)
    {
        var query = context.WebhookDeliveries
            .AsNoTracking()
            .Where(d => d.WebhookEndpointId == request.EndpointId);

        if (request.Status.HasValue)
            query = query.Where(d => d.Status == request.Status.Value);

        query = query.OrderByDescending(d => d.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(d => d.ToDto()).ToList();

        return Result.Success(PaginatedList<WebhookDeliveryDto>.Create(
            dtos.AsReadOnly(), totalCount, request.PageNumber, request.PageSize));
    }
}
