using Starter.Domain.Common;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Domain.Entities;

public sealed class DeliveryAttempt : BaseEntity
{
    public Guid DeliveryLogId { get; private set; }
    public int AttemptNumber { get; private set; }
    public NotificationChannel? Channel { get; private set; }
    public IntegrationType? IntegrationType { get; private set; }
    public ChannelProvider? Provider { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public string? ProviderResponse { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int? DurationMs { get; private set; }
    public DateTime AttemptedAt { get; private set; }

    private DeliveryAttempt() { }

    public static DeliveryAttempt Create(Guid deliveryLogId, int attemptNumber,
        NotificationChannel? channel, IntegrationType? integrationType, ChannelProvider? provider)
    {
        return new DeliveryAttempt
        {
            Id = Guid.NewGuid(),
            DeliveryLogId = deliveryLogId,
            AttemptNumber = attemptNumber,
            Channel = channel,
            IntegrationType = integrationType,
            Provider = provider,
            Status = DeliveryStatus.Sending,
            AttemptedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void RecordSuccess(string? providerResponse, int durationMs)
    {
        Status = DeliveryStatus.Delivered;
        ProviderResponse = providerResponse?.Length > 4000 ? providerResponse[..4000] : providerResponse;
        DurationMs = durationMs;
    }

    public void RecordFailure(string? errorMessage, string? providerResponse, int durationMs)
    {
        Status = DeliveryStatus.Failed;
        ErrorMessage = errorMessage;
        ProviderResponse = providerResponse?.Length > 4000 ? providerResponse[..4000] : providerResponse;
        DurationMs = durationMs;
    }
}
