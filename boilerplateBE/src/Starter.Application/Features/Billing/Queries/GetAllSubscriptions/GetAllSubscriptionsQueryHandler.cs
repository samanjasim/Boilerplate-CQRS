using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.Billing.DTOs;
using Starter.Domain.Billing.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetAllSubscriptions;

internal sealed class GetAllSubscriptionsQueryHandler(
    IApplicationDbContext context,
    IUsageTracker usageTracker) : IRequestHandler<GetAllSubscriptionsQuery, Result<PaginatedList<SubscriptionSummaryDto>>>
{
    public async Task<Result<PaginatedList<SubscriptionSummaryDto>>> Handle(
        GetAllSubscriptionsQuery request, CancellationToken cancellationToken)
    {
        var query = from sub in context.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking().Include(s => s.Plan)
                    join tenant in context.Tenants.IgnoreQueryFilters().AsNoTracking()
                        on sub.TenantId equals tenant.Id
                    select new { Subscription = sub, TenantName = tenant.Name, TenantSlug = tenant.Slug };

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(x => x.TenantName.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.Subscription.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var tenantIds = items.Select(x => x.Subscription.TenantId).ToList();

        var latestPayments = await context.PaymentRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => tenantIds.Contains(p.TenantId))
            .GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, Status = g.OrderByDescending(p => p.CreatedAt).First().Status })
            .ToDictionaryAsync(x => x.TenantId, x => x.Status, cancellationToken);

        var dtos = new List<SubscriptionSummaryDto>(items.Count);

        foreach (var item in items)
        {
            var sub = item.Subscription;
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

            dtos.Add(new SubscriptionSummaryDto(
                TenantId: sub.TenantId,
                TenantName: item.TenantName,
                TenantSlug: item.TenantSlug,
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
