namespace Starter.Application.Features.Webhooks.DTOs;

public sealed record WebhookEventTypeDto(string Type, string Resource, string Description);
