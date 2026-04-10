using Starter.Module.Webhooks.Domain.Enums;

namespace Starter.Module.Webhooks.Application.DTOs;

public sealed record WebhookDeliveryDto(
    Guid Id, string EventType, string RequestPayload,
    int? ResponseStatusCode, string? ResponseBody,
    WebhookDeliveryStatus Status, int? Duration,
    int AttemptCount, string? ErrorMessage, DateTime CreatedAt);
