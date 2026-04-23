using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.CancelDelegation;

public sealed record CancelDelegationCommand(Guid DelegationId) : IRequest<Result>;
