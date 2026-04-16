using System.Text.Json.Serialization;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Application.DTOs;

public sealed record RequiredNotificationDto(
    Guid Id,
    string Category,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] NotificationChannel Channel,
    DateTime CreatedAt);
