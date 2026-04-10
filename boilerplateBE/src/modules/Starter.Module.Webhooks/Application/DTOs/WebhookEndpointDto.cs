namespace Starter.Module.Webhooks.Application.DTOs;

public sealed record WebhookEndpointDto(
    Guid Id, string Url, string? Description, string[] Events,
    bool IsActive, DateTime CreatedAt, DateTime? ModifiedAt,
    string? LastDeliveryStatus, DateTime? LastDeliveryAt);
