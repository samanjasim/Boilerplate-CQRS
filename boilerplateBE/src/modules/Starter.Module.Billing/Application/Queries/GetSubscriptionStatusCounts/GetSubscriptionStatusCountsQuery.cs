using MediatR;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetSubscriptionStatusCounts;

public sealed record GetSubscriptionStatusCountsQuery() : IRequest<Result<SubscriptionStatusCountsDto>>;
