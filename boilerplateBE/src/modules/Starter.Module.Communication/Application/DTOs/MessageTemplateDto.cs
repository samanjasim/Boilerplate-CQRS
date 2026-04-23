using System.Text.Json.Serialization;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Application.DTOs;

public sealed record MessageTemplateDto(
    Guid Id,
    string Name,
    string ModuleSource,
    string Category,
    string? Description,
    string? SubjectTemplate,
    string BodyTemplate,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] NotificationChannel DefaultChannel,
    string[] AvailableChannels,
    bool IsSystem,
    bool HasOverride,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
