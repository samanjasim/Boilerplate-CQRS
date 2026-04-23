using System.Text.Json.Serialization;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Application.DTOs;

public sealed record IntegrationConfigDto(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] IntegrationType IntegrationType,
    string DisplayName,
    Dictionary<string, string>? MaskedCredentials,
    Dictionary<string, string>? ChannelMappings,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] IntegrationConfigStatus Status,
    DateTime? LastTestedAt,
    string? LastTestResult,
    DateTime CreatedAt,
    DateTime? ModifiedAt);
