namespace Starter.Application.Features.Webhooks.DTOs;

public sealed record WebhookAdminStatsDto(
    int TotalEndpoints,
    int ActiveEndpoints,
    int TotalDeliveries24h,
    int SuccessfulDeliveries24h,
    int FailedDeliveries24h,
    decimal SuccessRate24h);
