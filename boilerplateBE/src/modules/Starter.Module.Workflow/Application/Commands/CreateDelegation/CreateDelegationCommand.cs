using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.CreateDelegation;

public sealed record CreateDelegationCommand(
    Guid ToUserId,
    DateTime StartDate,
    DateTime EndDate) : IRequest<Result<Guid>>;
