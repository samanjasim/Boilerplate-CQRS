using System.Text.Json.Serialization;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Application.DTOs;

public sealed record ChannelConfigDto(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] NotificationChannel Channel,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ChannelProvider Provider,
    string DisplayName,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ChannelConfigStatus Status,
    bool IsDefault,
    DateTime? LastTestedAt,
    string? LastTestResult,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
