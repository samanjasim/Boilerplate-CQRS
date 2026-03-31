using MediatR;
using Starter.Application.Features.Billing.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Billing.Queries.GetUsage;

public sealed record GetUsageQuery(Guid? TenantId = null) : IRequest<Result<UsageDto>>;
