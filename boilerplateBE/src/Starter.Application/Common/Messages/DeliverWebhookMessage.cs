namespace Starter.Application.Common.Messages;

public sealed record DeliverWebhookMessage(Guid TenantId, string EventType, string Payload, DateTime OccurredAt);
