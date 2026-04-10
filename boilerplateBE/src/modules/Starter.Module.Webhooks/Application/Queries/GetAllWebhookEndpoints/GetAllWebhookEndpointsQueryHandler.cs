using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Domain.Tenants.Entities;
using Starter.Module.Webhooks.Domain.Enums;
using Starter.Module.Webhooks.Application.DTOs;
using Starter.Module.Webhooks.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Queries.GetAllWebhookEndpoints;

internal sealed class GetAllWebhookEndpointsQueryHandler(
    IApplicationDbContext appContext,
    WebhooksDbContext webhooksContext) : IRequestHandler<GetAllWebhookEndpointsQuery, Result<PaginatedList<WebhookAdminSummaryDto>>>
{
    public async Task<Result<PaginatedList<WebhookAdminSummaryDto>>> Handle(
        GetAllWebhookEndpointsQuery request, CancellationToken cancellationToken)
    {
        // Cross-context join is not possible — load endpoints from WebhooksDbContext,
        // then enrich with tenant name/slug from ApplicationDbContext via a separate query.
        var endpointsQuery = webhooksContext.WebhookEndpoints
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsQueryable();

        // If a search term is supplied, restrict to endpoints whose URL matches OR
        // whose tenant name matches (resolved via app context first).
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();

            var matchingTenantIds = await appContext.Set<Tenant>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => t.Name.ToLower().Contains(term))
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            endpointsQuery = endpointsQuery.Where(e =>
                e.Url.ToLower().Contains(term) || matchingTenantIds.Contains(e.TenantId));
        }

        var totalCount = await endpointsQuery.CountAsync(cancellationToken);

        var endpoints = await endpointsQuery
            .OrderByDescending(e => e.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var endpointIds = endpoints.Select(e => e.Id).ToList();
        var tenantIds = endpoints.Select(e => e.TenantId).Distinct().ToList();

        // Look up tenant name/slug from the app context for the page's tenant IDs.
        var tenantInfo = await appContext.Set<Tenant>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.Slug })
            .ToDictionaryAsync(t => t.Id, t => new { t.Name, t.Slug }, cancellationToken);

        var cutoff = DateTime.UtcNow.AddHours(-24);

        var deliveryStats = await webhooksContext.WebhookDeliveries
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

        var lastDeliveries = await webhooksContext.WebhookDeliveries
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

        var dtos = endpoints.Select(endpoint =>
        {
            deliveryStats.TryGetValue(endpoint.Id, out var stats);
            lastDeliveries.TryGetValue(endpoint.Id, out var last);
            tenantInfo.TryGetValue(endpoint.TenantId, out var tenant);

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
                TenantName: tenant?.Name ?? string.Empty,
                TenantSlug: tenant?.Slug ?? string.Empty,
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
