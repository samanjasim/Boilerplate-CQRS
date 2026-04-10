using MediatR;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetPlanById;

public sealed record GetPlanByIdQuery(Guid Id) : IRequest<Result<SubscriptionPlanDto>>;
