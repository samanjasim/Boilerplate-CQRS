using System.Text.Json;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Application.DTOs;

public static class TriggerRuleMapper
{
    public static TriggerRuleDto ToDto(this TriggerRule entity, string? templateName = null)
    {
        var channels = string.IsNullOrWhiteSpace(entity.ChannelSequenceJson)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(entity.ChannelSequenceJson) ?? [];

        return new TriggerRuleDto(
            Id: entity.Id,
            Name: entity.Name,
            EventName: entity.EventName,
            MessageTemplateId: entity.MessageTemplateId,
            MessageTemplateName: templateName,
            RecipientMode: entity.RecipientMode,
            ChannelSequence: channels,
            DelaySeconds: entity.DelaySeconds,
            Status: entity.Status,
            IntegrationTargetCount: entity.IntegrationTargets.Count,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }

    public static EventRegistrationDto ToDto(this EventRegistration entity)
    {
        return new EventRegistrationDto(
            Id: entity.Id,
            EventName: entity.EventName,
            ModuleSource: entity.ModuleSource,
            DisplayName: entity.DisplayName,
            Description: entity.Description);
    }
}
