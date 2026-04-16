using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.UpdateIntegrationConfig;

public sealed record UpdateIntegrationConfigCommand(
    Guid Id,
    string DisplayName,
    Dictionary<string, string> Credentials,
    string? ChannelMappingsJson) : IRequest<Result<IntegrationConfigDto>>;
