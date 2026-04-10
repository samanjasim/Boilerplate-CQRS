using MediatR;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetSubscription;

public sealed record GetSubscriptionQuery(Guid? TenantId = null) : IRequest<Result<TenantSubscriptionDto>>;
