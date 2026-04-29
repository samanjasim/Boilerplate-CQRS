using MediatR;
using Starter.Abstractions.Paging;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetAllSubscriptions;

// SuperAdmin admin-list. The handler intentionally calls IgnoreQueryFilters() and
// does NOT scope by currentUser.TenantId, so this query MUST NOT be exposed as an
// [AiTool] under a permission held by tenant Admins or Users (cross-tenant leak).
// The tenant-scoped agent-facing equivalent is GetSubscriptionQuery (get_my_subscription).
public sealed record GetAllSubscriptionsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null) : IRequest<Result<PaginatedList<SubscriptionSummaryDto>>>;
