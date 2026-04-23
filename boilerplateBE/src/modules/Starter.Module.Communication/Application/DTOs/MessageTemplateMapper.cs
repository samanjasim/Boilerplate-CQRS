using System.Text.Json;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Application.DTOs;

public static class MessageTemplateMapper
{
    public static MessageTemplateDto ToDto(this MessageTemplate entity, bool hasOverride = false)
    {
        var channels = string.IsNullOrWhiteSpace(entity.AvailableChannelsJson)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(entity.AvailableChannelsJson) ?? [];

        return new MessageTemplateDto(
            Id: entity.Id,
            Name: entity.Name,
            ModuleSource: entity.ModuleSource,
            Category: entity.Category,
            Description: entity.Description,
            SubjectTemplate: entity.SubjectTemplate,
            BodyTemplate: entity.BodyTemplate,
            DefaultChannel: entity.DefaultChannel,
            AvailableChannels: channels,
            IsSystem: entity.IsSystem,
            HasOverride: hasOverride,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }

    public static MessageTemplateDetailDto ToDetailDto(
        this MessageTemplate entity, MessageTemplateOverride? tenantOverride)
    {
        var channels = string.IsNullOrWhiteSpace(entity.AvailableChannelsJson)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(entity.AvailableChannelsJson) ?? [];

        var variableSchema = string.IsNullOrWhiteSpace(entity.VariableSchemaJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.VariableSchemaJson);

        var sampleVariables = string.IsNullOrWhiteSpace(entity.SampleVariablesJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.SampleVariablesJson);

        return new MessageTemplateDetailDto(
            Id: entity.Id,
            Name: entity.Name,
            ModuleSource: entity.ModuleSource,
            Category: entity.Category,
            Description: entity.Description,
            SubjectTemplate: entity.SubjectTemplate,
            BodyTemplate: entity.BodyTemplate,
            DefaultChannel: entity.DefaultChannel,
            AvailableChannels: channels,
            VariableSchema: variableSchema,
            SampleVariables: sampleVariables,
            IsSystem: entity.IsSystem,
            Override: tenantOverride?.ToDto(),
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }

    public static MessageTemplateOverrideDto ToDto(this MessageTemplateOverride entity)
    {
        return new MessageTemplateOverrideDto(
            Id: entity.Id,
            SubjectTemplate: entity.SubjectTemplate,
            BodyTemplate: entity.BodyTemplate,
            IsActive: entity.IsActive,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }
}
