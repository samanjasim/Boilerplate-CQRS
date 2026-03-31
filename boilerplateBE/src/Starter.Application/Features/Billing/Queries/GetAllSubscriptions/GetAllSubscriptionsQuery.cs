using MediatR;
using Starter.Application.Common.Models;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetAllSubscriptions;

public sealed record GetAllSubscriptionsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null) : IRequest<Result<PaginatedList<SubscriptionSummaryDto>>>;
