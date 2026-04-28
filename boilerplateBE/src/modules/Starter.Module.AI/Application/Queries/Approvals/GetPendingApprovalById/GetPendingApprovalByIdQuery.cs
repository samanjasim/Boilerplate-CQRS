using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Approvals.GetPendingApprovalById;

public sealed record GetPendingApprovalByIdQuery(Guid ApprovalId)
    : IRequest<Result<PendingApprovalDto>>;
