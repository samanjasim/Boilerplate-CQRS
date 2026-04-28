using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Approvals.DenyPendingAction;

public sealed record DenyPendingActionCommand(Guid ApprovalId, string Reason) : IRequest<Result>;
