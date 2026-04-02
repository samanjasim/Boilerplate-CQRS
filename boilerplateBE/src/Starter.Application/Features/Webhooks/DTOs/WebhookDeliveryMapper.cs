using Starter.Domain.Webhooks.Entities;

namespace Starter.Application.Features.Webhooks.DTOs;

public static class WebhookDeliveryMapper
{
    public static WebhookDeliveryDto ToDto(this WebhookDelivery entity)
    {
        return new WebhookDeliveryDto(
            Id: entity.Id,
            EventType: entity.EventType,
            RequestPayload: entity.RequestPayload,
            ResponseStatusCode: entity.ResponseStatusCode,
            ResponseBody: entity.ResponseBody,
            Status: entity.Status,
            Duration: entity.Duration,
            AttemptCount: entity.AttemptCount,
            ErrorMessage: entity.ErrorMessage,
            CreatedAt: entity.CreatedAt);
    }
}
