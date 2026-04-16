using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.DeleteChannelConfig;

public sealed record DeleteChannelConfigCommand(Guid Id) : IRequest<Result>;
