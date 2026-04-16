using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.SetDefaultChannelConfig;

public sealed record SetDefaultChannelConfigCommand(Guid Id) : IRequest<Result<ChannelConfigDto>>;
