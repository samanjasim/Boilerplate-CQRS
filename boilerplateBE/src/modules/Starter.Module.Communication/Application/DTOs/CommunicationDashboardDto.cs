namespace Starter.Module.Communication.Application.DTOs;

public sealed record CommunicationDashboardDto(
    long MessagesSentToday,
    long MessagesSentThisWeek,
    long MessagesSentThisMonth,
    double DeliverySuccessRate,
    Dictionary<string, long> ChannelBreakdown,
    long FailedDeliveries,
    long? QuotaUsed,
    long? QuotaLimit);
