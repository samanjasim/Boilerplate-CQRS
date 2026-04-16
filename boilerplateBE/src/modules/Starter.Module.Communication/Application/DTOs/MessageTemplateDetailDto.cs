using System.Text.Json.Serialization;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Application.DTOs;

public sealed record MessageTemplateDetailDto(
    Guid Id,
    string Name,
    string ModuleSource,
    string Category,
    string? Description,
    string? SubjectTemplate,
    string BodyTemplate,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] NotificationChannel DefaultChannel,
    string[] AvailableChannels,
    Dictionary<string, string>? VariableSchema,
    Dictionary<string, object>? SampleVariables,
    bool IsSystem,
    MessageTemplateOverrideDto? Override,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
