using Starter.Domain.Common;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Domain.Entities;

public sealed class DeliveryLog : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid? RecipientUserId { get; private set; }
    public string? RecipientAddress { get; private set; }
    public Guid? MessageTemplateId { get; private set; }
    public string TemplateName { get; private set; } = default!;
    public NotificationChannel? Channel { get; private set; }
    public IntegrationType? IntegrationType { get; private set; }
    public ChannelProvider? Provider { get; private set; }
    public string? Subject { get; private set; }
    public string? BodyPreview { get; private set; }
    public string? VariablesJson { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? TriggerRuleId { get; private set; }
    public int? TotalDurationMs { get; private set; }

    private readonly List<DeliveryAttempt> _attempts = [];
    public IReadOnlyCollection<DeliveryAttempt> Attempts => _attempts.AsReadOnly();

    private DeliveryLog() { }

    public static DeliveryLog Create(Guid tenantId, Guid? recipientUserId, string? recipientAddress,
        Guid? messageTemplateId, string templateName, NotificationChannel? channel,
        IntegrationType? integrationType, string? subject, string? bodyPreview,
        string? variablesJson, Guid? triggerRuleId = null)
    {
        return new DeliveryLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientUserId = recipientUserId,
            RecipientAddress = recipientAddress,
            MessageTemplateId = messageTemplateId,
            TemplateName = templateName,
            Channel = channel,
            IntegrationType = integrationType,
            Subject = subject,
            BodyPreview = bodyPreview?.Length > 1000 ? bodyPreview[..1000] : bodyPreview,
            VariablesJson = variablesJson,
            Status = DeliveryStatus.Pending,
            TriggerRuleId = triggerRuleId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkQueued() { Status = DeliveryStatus.Queued; ModifiedAt = DateTime.UtcNow; }
    public void MarkSending(ChannelProvider provider) { Status = DeliveryStatus.Sending; Provider = provider; ModifiedAt = DateTime.UtcNow; }

    public void MarkDelivered(string? providerMessageId, int durationMs)
    {
        Status = DeliveryStatus.Delivered;
        ProviderMessageId = providerMessageId;
        TotalDurationMs = durationMs;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string? errorMessage)
    {
        Status = DeliveryStatus.Failed;
        ErrorMessage = errorMessage;
        ModifiedAt = DateTime.UtcNow;
    }

    public void MarkBounced(string? errorMessage)
    {
        Status = DeliveryStatus.Bounced;
        ErrorMessage = errorMessage;
        ModifiedAt = DateTime.UtcNow;
    }

    public void AddAttempt(DeliveryAttempt attempt) => _attempts.Add(attempt);
}
