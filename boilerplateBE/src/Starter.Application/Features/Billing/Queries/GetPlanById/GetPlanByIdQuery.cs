using MediatR;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetPlanById;

public sealed record GetPlanByIdQuery(Guid Id) : IRequest<Result<SubscriptionPlanDto>>;
