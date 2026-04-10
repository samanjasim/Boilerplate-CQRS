using MediatR;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetPlanOptions;

public sealed record GetPlanOptionsQuery : IRequest<Result<List<PlanOptionDto>>>;
