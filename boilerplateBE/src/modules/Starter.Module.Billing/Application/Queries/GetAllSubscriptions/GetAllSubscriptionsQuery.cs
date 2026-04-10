using MediatR;
using Starter.Application.Common.Models;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetAllSubscriptions;

public sealed record GetAllSubscriptionsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null) : IRequest<Result<PaginatedList<SubscriptionSummaryDto>>>;
