using System.Text.Json.Serialization;
using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.CreateIntegrationConfig;

public sealed record CreateIntegrationConfigCommand(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] IntegrationType IntegrationType,
    string DisplayName,
    Dictionary<string, string> Credentials,
    string? ChannelMappingsJson) : IRequest<Result<IntegrationConfigDto>>;
