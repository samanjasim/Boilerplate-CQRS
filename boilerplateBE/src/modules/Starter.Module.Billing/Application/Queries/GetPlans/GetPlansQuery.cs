using MediatR;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetPlans;

public sealed record GetPlansQuery(
    bool PublicOnly = false,
    bool IncludeInactive = false) : IRequest<Result<List<SubscriptionPlanDto>>>;
