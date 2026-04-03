using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.Webhooks.DTOs;
using Starter.Domain.Webhooks.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Queries.GetAllWebhookEndpoints;

internal sealed class GetAllWebhookEndpointsQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetAllWebhookEndpointsQuery, Result<PaginatedList<WebhookAdminSummaryDto>>>
{
    public async Task<Result<PaginatedList<WebhookAdminSummaryDto>>> Handle(
        GetAllWebhookEndpointsQuery request, CancellationToken cancellationToken)
    {
        var query = from endpoint in context.WebhookEndpoints.IgnoreQueryFilters().AsNoTracking()
                    join tenant in context.Tenants.IgnoreQueryFilters().AsNoTracking()
                        on endpoint.TenantId equals tenant.Id
                    select new { Endpoint = endpoint, TenantName = tenant.Name, TenantSlug = tenant.Slug };

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(x => x.TenantName.ToLower().Contains(term)
                                     || x.Endpoint.Url.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.Endpoint.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var endpointIds = items.Select(x => x.Endpoint.Id).ToList();

        var cutoff = DateTime.UtcNow.AddHours(-24);

        var deliveryStats = await context.WebhookDeliveries
            .IgnoreQueryFilters().AsNoTracking()
            .Where(d => endpointIds.Contains(d.WebhookEndpointId) && d.CreatedAt >= cutoff)
            .GroupBy(d => d.WebhookEndpointId)
            .Select(g => new
            {
                EndpointId = g.Key,
                Total = g.Count(),
                Success = g.Count(d => d.Status == WebhookDeliveryStatus.Success),
                Failed = g.Count(d => d.Status == WebhookDeliveryStatus.Failed)
            })
            .ToDictionaryAsync(x => x.EndpointId, cancellationToken);

        var lastDeliveries = await context.WebhookDeliveries
            .IgnoreQueryFilters().AsNoTracking()
            .Where(d => endpointIds.Contains(d.WebhookEndpointId))
            .GroupBy(d => d.WebhookEndpointId)
            .Select(g => new
            {
                EndpointId = g.Key,
                Status = g.OrderByDescending(d => d.CreatedAt).First().Status,
                At = g.OrderByDescending(d => d.CreatedAt).First().CreatedAt
            })
            .ToDictionaryAsync(x => x.EndpointId, cancellationToken);

        var dtos = items.Select(item =>
        {
            var endpoint = item.Endpoint;
            deliveryStats.TryGetValue(endpoint.Id, out var stats);
            lastDeliveries.TryGetValue(endpoint.Id, out var last);

            var events = string.IsNullOrWhiteSpace(endpoint.Events)
                ? Array.Empty<string>()
                : JsonSerializer.Deserialize<string[]>(endpoint.Events) ?? Array.Empty<string>();

            return new WebhookAdminSummaryDto(
                Id: endpoint.Id,
                Url: endpoint.Url,
                Description: endpoint.Description,
                Events: events,
                IsActive: endpoint.IsActive,
                TenantId: endpoint.TenantId,
                TenantName: item.TenantName,
                TenantSlug: item.TenantSlug,
                CreatedAt: endpoint.CreatedAt,
                DeliveriesLast24h: stats?.Total ?? 0,
                SuccessfulLast24h: stats?.Success ?? 0,
                FailedLast24h: stats?.Failed ?? 0,
                LastDeliveryStatus: last?.Status.ToString(),
                LastDeliveryAt: last?.At);
        }).ToList();

        return Result.Success(PaginatedList<WebhookAdminSummaryDto>.Create(
            dtos.AsReadOnly(), totalCount, request.PageNumber, request.PageSize));
    }
}
