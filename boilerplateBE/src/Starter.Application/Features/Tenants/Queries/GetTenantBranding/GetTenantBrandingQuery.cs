using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Queries.GetTenantBranding;

public sealed record GetTenantBrandingQuery(
    string? Slug,
    Guid? TenantId) : IRequest<Result<TenantBrandingDto>>;
