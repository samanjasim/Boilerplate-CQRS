using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.DeleteIntegrationConfig;

public sealed record DeleteIntegrationConfigCommand(Guid Id) : IRequest<Result>;
