using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.TestIntegrationConfig;

public sealed record TestIntegrationConfigCommand(Guid Id) : IRequest<Result<TestIntegrationConfigResponse>>;

public sealed record TestIntegrationConfigResponse(bool Success, string? Message);
