using MediatR;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetPlanOptions;

public sealed record GetPlanOptionsQuery : IRequest<Result<List<PlanOptionDto>>>;
