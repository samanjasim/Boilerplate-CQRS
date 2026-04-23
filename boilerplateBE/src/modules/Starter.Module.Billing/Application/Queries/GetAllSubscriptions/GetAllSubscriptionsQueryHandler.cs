using Starter.Abstractions.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Domain.Tenants.Entities;
using Starter.Module.Billing.Domain.Entities;
using Starter.Module.Billing.Domain.Enums;
using Starter.Module.Billing.Application.DTOs;
using Starter.Module.Billing.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetAllSubscriptions;

internal sealed class GetAllSubscriptionsQueryHandler(
    IApplicationDbContext appContext,
    BillingDbContext billingContext,
    IUsageTracker usageTracker) : IRequestHandler<GetAllSubscriptionsQuery, Result<PaginatedList<SubscriptionSummaryDto>>>
{
    public async Task<Result<PaginatedList<SubscriptionSummaryDto>>> Handle(
        GetAllSubscriptionsQuery request, CancellationToken cancellationToken)
    {
        // Cross-context join is not possible — load subscriptions from BillingDbContext,
        // then enrich with tenant name/slug from ApplicationDbContext via a separate query.
        var subsQuery = billingContext.TenantSubscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(s => s.Plan)
            .AsQueryable();

        // If a search term is supplied, restrict to tenant IDs whose name matches
        // by querying the app context first.
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            var matchingTenantIds = await appContext.Set<Tenant>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => t.Name.ToLower().Contains(term))
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            subsQuery = subsQuery.Where(s => matchingTenantIds.Contains(s.TenantId));
        }

        var totalCount = await subsQuery.CountAsync(cancellationToken);

        var subscriptions = await subsQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var tenantIds = subscriptions.Select(s => s.TenantId).ToList();

        // Look up tenant name/slug from the app context for the page's tenant IDs.
        var tenantInfo = await appContext.Set<Tenant>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.Slug })
            .ToDictionaryAsync(t => t.Id, t => new { t.Name, t.Slug }, cancellationToken);

        var latestPayments = await billingContext.PaymentRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => tenantIds.Contains(p.TenantId))
            .GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, Status = g.OrderByDescending(p => p.CreatedAt).First().Status })
            .ToDictionaryAsync(x => x.TenantId, x => x.Status, cancellationToken);

        var dtos = new List<SubscriptionSummaryDto>(subscriptions.Count);

        foreach (var sub in subscriptions)
        {
            var usersCount = await usageTracker.GetAsync(sub.TenantId, "users", cancellationToken);
            var storageBytes = await usageTracker.GetAsync(sub.TenantId, "storage_bytes", cancellationToken);
            var webhooksCount = await usageTracker.GetAsync(sub.TenantId, "webhooks", cancellationToken);
            var storageUsedMb = storageBytes / (1024 * 1024);

            var maxUsers = 0;
            long maxStorageMb = 0;
            var maxWebhooks = 0;

            var featureEntries = PlanFeatureEntry.ParseFeatures(sub.Plan.Features);
            if (featureEntries is not null)
            {
                var featureMap = featureEntries.ToDictionary(e => e.Key, e => e.Value);
                if (featureMap.TryGetValue("users.max_count", out var usersMaxStr) && int.TryParse(usersMaxStr, out var usersMaxVal))
                    maxUsers = usersMaxVal;
                if (featureMap.TryGetValue("files.max_storage_mb", out var storageMaxStr) && long.TryParse(storageMaxStr, out var storageMaxVal))
                    maxStorageMb = storageMaxVal;
                if (featureMap.TryGetValue("webhooks.max_count", out var webhooksMaxStr) && int.TryParse(webhooksMaxStr, out var webhooksMaxVal))
                    maxWebhooks = webhooksMaxVal;
            }

            latestPayments.TryGetValue(sub.TenantId, out var paymentStatus);
            tenantInfo.TryGetValue(sub.TenantId, out var tenant);

            dtos.Add(new SubscriptionSummaryDto(
                TenantId: sub.TenantId,
                TenantName: tenant?.Name ?? string.Empty,
                TenantSlug: tenant?.Slug ?? string.Empty,
                SubscriptionPlanId: sub.SubscriptionPlanId,
                PlanName: sub.Plan.Name,
                PlanSlug: sub.Plan.Slug,
                Status: sub.Status,
                BillingInterval: sub.BillingInterval,
                CurrentPeriodStart: sub.CurrentPeriodStart,
                CurrentPeriodEnd: sub.CurrentPeriodEnd,
                UsersCount: usersCount,
                MaxUsers: maxUsers,
                StorageUsedMb: storageUsedMb,
                MaxStorageMb: maxStorageMb,
                LatestPaymentStatus: latestPayments.ContainsKey(sub.TenantId) ? paymentStatus : null,
                CreatedAt: sub.CreatedAt,
                WebhooksCount: webhooksCount,
                MaxWebhooks: maxWebhooks));
        }

        return Result.Success(PaginatedList<SubscriptionSummaryDto>.Create(dtos.AsReadOnly(), totalCount, request.PageNumber, request.PageSize));
    }
}
