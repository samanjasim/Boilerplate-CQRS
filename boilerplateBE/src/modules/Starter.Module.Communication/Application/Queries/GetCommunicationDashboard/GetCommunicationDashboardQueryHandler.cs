using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Enums;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetCommunicationDashboard;

internal sealed class GetCommunicationDashboardQueryHandler(
    CommunicationDbContext context)
    : IRequestHandler<GetCommunicationDashboardQuery, Result<CommunicationDashboardDto>>
{
    public async Task<Result<CommunicationDashboardDto>> Handle(
        GetCommunicationDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var startOfDay = now.Date;
        var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var logs = context.DeliveryLogs.AsNoTracking();

        var messagesToday = await logs
            .CountAsync(d => d.CreatedAt >= startOfDay, cancellationToken);

        var messagesThisWeek = await logs
            .CountAsync(d => d.CreatedAt >= startOfWeek, cancellationToken);

        var messagesThisMonth = await logs
            .CountAsync(d => d.CreatedAt >= startOfMonth, cancellationToken);

        var delivered = await logs
            .CountAsync(d => d.Status == DeliveryStatus.Delivered, cancellationToken);

        var failed = await logs
            .CountAsync(d => d.Status == DeliveryStatus.Failed || d.Status == DeliveryStatus.Bounced, cancellationToken);

        var totalTerminal = delivered + failed;
        var successRate = totalTerminal > 0
            ? Math.Round((double)delivered / totalTerminal * 100, 1)
            : 0;

        var channelBreakdown = await logs
            .Where(d => d.Channel != null)
            .GroupBy(d => d.Channel!.Value)
            .Select(g => new { Channel = g.Key.ToString(), Count = (long)g.Count() })
            .ToDictionaryAsync(g => g.Channel, g => g.Count, cancellationToken);

        // Add integration type counts for non-channel messages
        var integrationBreakdown = await logs
            .Where(d => d.Channel == null && d.IntegrationType != null)
            .GroupBy(d => d.IntegrationType!.Value)
            .Select(g => new { Type = g.Key.ToString(), Count = (long)g.Count() })
            .ToDictionaryAsync(g => g.Type, g => g.Count, cancellationToken);

        foreach (var kvp in integrationBreakdown)
            channelBreakdown[kvp.Key] = kvp.Value;

        var dashboard = new CommunicationDashboardDto(
            MessagesSentToday: messagesToday,
            MessagesSentThisWeek: messagesThisWeek,
            MessagesSentThisMonth: messagesThisMonth,
            DeliverySuccessRate: successRate,
            ChannelBreakdown: channelBreakdown,
            FailedDeliveries: failed,
            QuotaUsed: null,
            QuotaLimit: null);

        return Result.Success(dashboard);
    }
}
