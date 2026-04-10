using MediatR;
using Starter.Module.Billing.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Billing.Application.Queries.GetUsage;

public sealed record GetUsageQuery(Guid? TenantId = null) : IRequest<Result<UsageDto>>;
