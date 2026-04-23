using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Application.DTOs;

public static class DeliveryLogMapper
{
    public static DeliveryLogDto ToDto(this DeliveryLog entity, int attemptCount)
    {
        return new DeliveryLogDto(
            Id: entity.Id,
            RecipientUserId: entity.RecipientUserId,
            RecipientAddress: entity.RecipientAddress,
            TemplateName: entity.TemplateName,
            Channel: entity.Channel,
            IntegrationType: entity.IntegrationType,
            Provider: entity.Provider,
            Subject: entity.Subject,
            BodyPreview: entity.BodyPreview,
            Status: entity.Status,
            ProviderMessageId: entity.ProviderMessageId,
            ErrorMessage: entity.ErrorMessage,
            TotalDurationMs: entity.TotalDurationMs,
            AttemptCount: attemptCount,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }

    public static DeliveryLogDetailDto ToDetailDto(this DeliveryLog entity)
    {
        return new DeliveryLogDetailDto(
            Id: entity.Id,
            RecipientUserId: entity.RecipientUserId,
            RecipientAddress: entity.RecipientAddress,
            MessageTemplateId: entity.MessageTemplateId,
            TemplateName: entity.TemplateName,
            Channel: entity.Channel,
            IntegrationType: entity.IntegrationType,
            Provider: entity.Provider,
            Subject: entity.Subject,
            BodyPreview: entity.BodyPreview,
            Status: entity.Status,
            ProviderMessageId: entity.ProviderMessageId,
            ErrorMessage: entity.ErrorMessage,
            TriggerRuleId: entity.TriggerRuleId,
            TotalDurationMs: entity.TotalDurationMs,
            Attempts: entity.Attempts.Select(a => a.ToDto()).OrderBy(a => a.AttemptNumber).ToList(),
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }

    public static DeliveryAttemptDto ToDto(this DeliveryAttempt entity)
    {
        return new DeliveryAttemptDto(
            Id: entity.Id,
            AttemptNumber: entity.AttemptNumber,
            Channel: entity.Channel,
            IntegrationType: entity.IntegrationType,
            Provider: entity.Provider,
            Status: entity.Status,
            ProviderResponse: entity.ProviderResponse,
            ErrorMessage: entity.ErrorMessage,
            DurationMs: entity.DurationMs,
            AttemptedAt: entity.AttemptedAt);
    }
}
