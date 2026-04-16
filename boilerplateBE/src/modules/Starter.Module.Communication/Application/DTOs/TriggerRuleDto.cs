using System.Text.Json.Serialization;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Application.DTOs;

public sealed record TriggerRuleDto(
    Guid Id,
    string Name,
    string EventName,
    Guid MessageTemplateId,
    string? MessageTemplateName,
    string RecipientMode,
    string[] ChannelSequence,
    int DelaySeconds,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TriggerRuleStatus Status,
    int IntegrationTargetCount,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
