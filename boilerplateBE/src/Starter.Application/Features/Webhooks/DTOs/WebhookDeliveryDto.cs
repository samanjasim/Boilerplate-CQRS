using Starter.Domain.Webhooks.Enums;

namespace Starter.Application.Features.Webhooks.DTOs;

public sealed record WebhookDeliveryDto(
    Guid Id, string EventType, string RequestPayload,
    int? ResponseStatusCode, string? ResponseBody,
    WebhookDeliveryStatus Status, int? Duration,
    int AttemptCount, string? ErrorMessage, DateTime CreatedAt);
