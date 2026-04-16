using System.Text.Json.Serialization;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Application.DTOs;

public sealed record DeliveryAttemptDto(
    Guid Id,
    int AttemptNumber,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] NotificationChannel? Channel,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] IntegrationType? IntegrationType,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ChannelProvider? Provider,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DeliveryStatus Status,
    string? ProviderResponse,
    string? ErrorMessage,
    int? DurationMs,
    DateTime AttemptedAt);
