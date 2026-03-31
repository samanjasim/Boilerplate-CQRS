using MediatR;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetPlans;

public sealed record GetPlansQuery(
    bool PublicOnly = false,
    bool IncludeInactive = false) : IRequest<Result<List<SubscriptionPlanDto>>>;
