namespace Starter.Module.Communication.Application.Messages;

public sealed record DispatchIntegrationMessage(
    Guid DeliveryLogId,
    Guid TenantId,
    Guid IntegrationConfigId,
    string TargetChannelId,
    string Message,
    DateTime QueuedAt);
