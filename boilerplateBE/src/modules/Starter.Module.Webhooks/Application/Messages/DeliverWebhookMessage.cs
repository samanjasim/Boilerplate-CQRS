namespace Starter.Module.Webhooks.Application.Messages;

public sealed record DeliverWebhookMessage(Guid TenantId, string EventType, string Payload, DateTime OccurredAt);
