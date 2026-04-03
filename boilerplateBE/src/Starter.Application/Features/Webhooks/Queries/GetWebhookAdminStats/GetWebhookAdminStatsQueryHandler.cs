using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Webhooks.DTOs;
using Starter.Domain.Webhooks.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Queries.GetWebhookAdminStats;

internal sealed class GetWebhookAdminStatsQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetWebhookAdminStatsQuery, Result<WebhookAdminStatsDto>>
{
    public async Task<Result<WebhookAdminStatsDto>> Handle(
        GetWebhookAdminStatsQuery request, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);

        var totalEndpoints = await context.WebhookEndpoints
            .IgnoreQueryFilters()
            .CountAsync(cancellationToken);

        var activeEndpoints = await context.WebhookEndpoints
            .IgnoreQueryFilters()
            .CountAsync(e => e.IsActive, cancellationToken);

        var deliveries24h = await context.WebhookDeliveries
            .IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.CreatedAt >= cutoff)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Success = g.Count(d => d.Status == WebhookDeliveryStatus.Success),
                Failed = g.Count(d => d.Status == WebhookDeliveryStatus.Failed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var total = deliveries24h?.Total ?? 0;
        var success = deliveries24h?.Success ?? 0;
        var failed = deliveries24h?.Failed ?? 0;
        var rate = total > 0 ? Math.Round((decimal)success / total * 100, 1) : 0;

        return Result.Success(new WebhookAdminStatsDto(
            TotalEndpoints: totalEndpoints,
            ActiveEndpoints: activeEndpoints,
            TotalDeliveries24h: total,
            SuccessfulDeliveries24h: success,
            FailedDeliveries24h: failed,
            SuccessRate24h: rate));
    }
}
