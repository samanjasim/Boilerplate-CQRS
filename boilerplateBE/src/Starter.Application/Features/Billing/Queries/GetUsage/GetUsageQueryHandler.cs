using MediatR;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Billing.DTOs;
using Starter.Domain.Billing.Errors;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetUsage;

internal sealed class GetUsageQueryHandler(
    ICurrentUserService currentUser,
    IUsageTracker usageTracker,
    IFeatureFlagService featureFlagService) : IRequestHandler<GetUsageQuery, Result<UsageDto>>
{
    public async Task<Result<UsageDto>> Handle(
        GetUsageQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? currentUser.TenantId;

        if (!tenantId.HasValue)
            return Result.Failure<UsageDto>(BillingErrors.SubscriptionNotFound);

        var counters = await usageTracker.GetAllAsync(tenantId.Value, cancellationToken);

        var maxUsers = await featureFlagService.GetValueAsync<int>("users.max_count", cancellationToken);
        var maxStorageMb = await featureFlagService.GetValueAsync<int>("files.max_storage_mb", cancellationToken);
        var maxApiKeys = await featureFlagService.GetValueAsync<int>("api_keys.max_count", cancellationToken);
        var maxReports = await featureFlagService.GetValueAsync<int>("reports.max_concurrent", cancellationToken);
        var maxWebhooks = await featureFlagService.GetValueAsync<int>("webhooks.max_count", cancellationToken);

        counters.TryGetValue("users", out var users);
        counters.TryGetValue("storage_bytes", out var storageBytes);
        counters.TryGetValue("api_keys", out var apiKeys);
        counters.TryGetValue("reports_active", out var reportsActive);
        counters.TryGetValue("webhooks", out var webhooks);

        var dto = new UsageDto(
            Users: users,
            StorageBytes: storageBytes,
            ApiKeys: apiKeys,
            ReportsActive: reportsActive,
            Webhooks: (int)webhooks,
            MaxUsers: maxUsers,
            MaxStorageBytes: (long)maxStorageMb * 1024 * 1024,
            MaxApiKeys: maxApiKeys,
            MaxReports: maxReports,
            MaxWebhooks: maxWebhooks);

        return Result.Success(dto);
    }
}
