using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.UpdateChannelConfig;

public sealed record UpdateChannelConfigCommand(
    Guid Id,
    string DisplayName,
    Dictionary<string, string> Credentials) : IRequest<Result<ChannelConfigDto>>;
