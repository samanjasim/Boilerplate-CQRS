namespace Starter.Module.Webhooks.Application.DTOs;

public sealed record WebhookEventTypeDto(string Type, string Resource, string Description);
