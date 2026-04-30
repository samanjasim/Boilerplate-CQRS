using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetDeliveryStatusCounts;

internal sealed class GetDeliveryStatusCountsQueryHandler(
    CommunicationDbContext context,
    TimeProvider timeProvider)
    : IRequestHandler<GetDeliveryStatusCountsQuery, Result<DeliveryStatusCountsDto>>
{
    private const int MinWindowDays = 1;
    private const int MaxWindowDays = 90;

    public async Task<Result<DeliveryStatusCountsDto>> Handle(
        GetDeliveryStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        var clampedWindow = Math.Clamp(request.WindowDays, MinWindowDays, MaxWindowDays);
        var cutoff = timeProvider.GetUtcNow().AddDays(-clampedWindow).UtcDateTime;

        var counts = await context.DeliveryLogs
            .AsNoTracking()
            .Where(d => d.CreatedAt >= cutoff)
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byStatus = counts.ToDictionary(c => c.Status, c => c.Count);

        var dto = new DeliveryStatusCountsDto(
            Delivered: byStatus.GetValueOrDefault(DeliveryStatus.Delivered),
            Failed:    byStatus.GetValueOrDefault(DeliveryStatus.Failed),
            Pending:   byStatus.GetValueOrDefault(DeliveryStatus.Pending)
                       + byStatus.GetValueOrDefault(DeliveryStatus.Queued)
                       + byStatus.GetValueOrDefault(DeliveryStatus.Sending),
            Bounced:   byStatus.GetValueOrDefault(DeliveryStatus.Bounced),
            WindowDays: clampedWindow);

        return Result.Success(dto);
    }
}
