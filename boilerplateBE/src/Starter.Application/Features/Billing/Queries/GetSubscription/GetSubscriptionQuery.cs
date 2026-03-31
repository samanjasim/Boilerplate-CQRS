using MediatR;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetSubscription;

public sealed record GetSubscriptionQuery(Guid? TenantId = null) : IRequest<Result<TenantSubscriptionDto>>;
