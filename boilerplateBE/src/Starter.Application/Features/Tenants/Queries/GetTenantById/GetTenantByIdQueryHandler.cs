using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Tenants.DTOs;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Queries.GetTenantById;

internal sealed class GetTenantByIdQueryHandler(
    IApplicationDbContext context,
    IFileService fileService) : IRequestHandler<GetTenantByIdQuery, Result<TenantDto>>
{
    public async Task<Result<TenantDto>> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure<TenantDto>(TenantErrors.NotFound(request.Id));

        string? logoUrl = null;
        string? faviconUrl = null;

        if (tenant.LogoFileId.HasValue)
            logoUrl = await fileService.GetUrlAsync(tenant.LogoFileId.Value, cancellationToken);

        if (tenant.FaviconFileId.HasValue)
            faviconUrl = await fileService.GetUrlAsync(tenant.FaviconFileId.Value, cancellationToken);

        string? defaultRoleName = null;
        if (tenant.DefaultRegistrationRoleId is not null)
        {
            defaultRoleName = await context.Roles
                .IgnoreQueryFilters()
                .Where(r => r.Id == tenant.DefaultRegistrationRoleId.Value)
                .Select(r => r.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Result.Success(tenant.ToDto(logoUrl, faviconUrl, defaultRoleName));
    }
}
