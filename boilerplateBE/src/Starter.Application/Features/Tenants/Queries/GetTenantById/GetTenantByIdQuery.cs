using Starter.Application.Features.Tenants.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Queries.GetTenantById;

public sealed record GetTenantByIdQuery(Guid Id) : IRequest<Result<TenantDto>>;
