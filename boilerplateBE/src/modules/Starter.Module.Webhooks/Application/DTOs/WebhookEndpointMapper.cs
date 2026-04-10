using System.Text.Json;
using Starter.Module.Webhooks.Domain.Entities;

namespace Starter.Module.Webhooks.Application.DTOs;

public static class WebhookEndpointMapper
{
    public static WebhookEndpointDto ToDto(
        this WebhookEndpoint entity,
        string? lastDeliveryStatus = null,
        DateTime? lastDeliveryAt = null)
    {
        var events = string.IsNullOrWhiteSpace(entity.Events)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(entity.Events) ?? Array.Empty<string>();

        return new WebhookEndpointDto(
            Id: entity.Id,
            Url: entity.Url,
            Description: entity.Description,
            Events: events,
            IsActive: entity.IsActive,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt,
            LastDeliveryStatus: lastDeliveryStatus,
            LastDeliveryAt: lastDeliveryAt);
    }
}
