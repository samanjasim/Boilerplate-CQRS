using MediatR;
using Starter.Abstractions.Paging;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Approvals.GetPendingApprovals;

public sealed record GetPendingApprovalsQuery(
    PendingApprovalStatus? Status = null,
    Guid? AssistantId = null,
    int Page = 1,
    int PageSize = 20)
    : IRequest<Result<PaginatedList<PendingApprovalDto>>>;
