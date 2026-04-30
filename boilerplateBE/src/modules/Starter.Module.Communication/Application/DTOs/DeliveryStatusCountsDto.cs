namespace Starter.Module.Communication.Application.DTOs;

public sealed record DeliveryStatusCountsDto(
    int Delivered,
    int Failed,
    int Pending,
    int Bounced,
    int WindowDays);
