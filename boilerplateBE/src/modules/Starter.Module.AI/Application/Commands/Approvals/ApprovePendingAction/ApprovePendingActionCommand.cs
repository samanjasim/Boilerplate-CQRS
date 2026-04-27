using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Approvals.ApprovePendingAction;

public sealed record ApprovePendingActionCommand(Guid ApprovalId, string? Note) : IRequest<Result<object?>>;
