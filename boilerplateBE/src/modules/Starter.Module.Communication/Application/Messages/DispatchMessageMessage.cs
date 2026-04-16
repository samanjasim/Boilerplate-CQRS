using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Application.Messages;

public sealed record DispatchMessageMessage(
    Guid DeliveryLogId,
    Guid TenantId,
    Guid? RecipientUserId,
    string RecipientAddress,
    string TemplateName,
    string? RenderedSubject,
    string RenderedBody,
    NotificationChannel Channel,
    string[] FallbackChannels,
    int CurrentFallbackIndex,
    DateTime QueuedAt);
