using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.TestChannelConfig;

public sealed record TestChannelConfigCommand(Guid Id) : IRequest<Result<TestChannelConfigResponse>>;

public sealed record TestChannelConfigResponse(bool Success, string? Message);
