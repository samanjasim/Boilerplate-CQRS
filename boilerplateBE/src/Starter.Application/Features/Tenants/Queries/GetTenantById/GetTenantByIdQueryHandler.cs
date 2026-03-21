using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Tenants.DTOs;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Queries.GetTenantById;

internal sealed class GetTenantByIdQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetTenantByIdQuery, Result<TenantDto>>
{
    public async Task<Result<TenantDto>> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure<TenantDto>(TenantErrors.NotFound(request.Id));

        return Result.Success(tenant.ToDto());
    }
}
