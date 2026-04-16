using System.Text.Json.Serialization;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Application.DTOs;

public sealed record DeliveryLogDto(
    Guid Id,
    Guid? RecipientUserId,
    string? RecipientAddress,
    string TemplateName,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] NotificationChannel? Channel,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] IntegrationType? IntegrationType,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] ChannelProvider? Provider,
    string? Subject,
    string? BodyPreview,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DeliveryStatus Status,
    string? ProviderMessageId,
    string? ErrorMessage,
    int? TotalDurationMs,
    int AttemptCount,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
