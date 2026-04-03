using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.Webhooks.DTOs;
using Starter.Domain.Webhooks.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Queries.GetWebhookDeliveriesAdmin;

internal sealed class GetWebhookDeliveriesAdminQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetWebhookDeliveriesAdminQuery, Result<PaginatedList<WebhookDeliveryDto>>>
{
    public async Task<Result<PaginatedList<WebhookDeliveryDto>>> Handle(
        GetWebhookDeliveriesAdminQuery request, CancellationToken cancellationToken)
    {
        var endpointExists = await context.WebhookEndpoints
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(e => e.Id == request.EndpointId, cancellationToken);

        if (!endpointExists)
            return Result.Failure<PaginatedList<WebhookDeliveryDto>>(WebhookErrors.EndpointNotFound);

        var query = context.WebhookDeliveries
            .IgnoreQueryFilters()
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
