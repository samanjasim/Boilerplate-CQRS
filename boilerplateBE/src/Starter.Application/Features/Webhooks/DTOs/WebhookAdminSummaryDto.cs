namespace Starter.Application.Features.Webhooks.DTOs;

public sealed record WebhookAdminSummaryDto(
    Guid Id,
    string Url,
    string? Description,
    string[] Events,
    bool IsActive,
    Guid TenantId,
    string TenantName,
    string? TenantSlug,
    DateTime CreatedAt,
    int DeliveriesLast24h,
    int SuccessfulLast24h,
    int FailedLast24h,
    string? LastDeliveryStatus,
    DateTime? LastDeliveryAt);
