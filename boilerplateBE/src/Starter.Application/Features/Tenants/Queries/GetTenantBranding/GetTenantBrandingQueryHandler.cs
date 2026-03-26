using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Entities;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Queries.GetTenantBranding;

internal sealed class GetTenantBrandingQueryHandler(
    IApplicationDbContext context,
    IFileService fileService) : IRequestHandler<GetTenantBrandingQuery, Result<TenantBrandingDto>>
{
    public async Task<Result<TenantBrandingDto>> Handle(GetTenantBrandingQuery request, CancellationToken cancellationToken)
    {
        // Reject obviously invalid slugs early (DB max length = 100)
        if (!string.IsNullOrWhiteSpace(request.Slug) && request.Slug.Length > 100)
            return Result.Failure<TenantBrandingDto>(Error.NotFound("Tenant.NotFound", "Tenant not found."));

        Tenant? tenant;

        if (request.TenantId.HasValue)
        {
            tenant = await context.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == request.TenantId.Value, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            tenant = await context.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == request.Slug, cancellationToken);
        }
        else
        {
            return Result.Failure<TenantBrandingDto>(
                Error.Validation("Tenant.BrandingQuery", "Either slug or tenant ID must be provided."));
        }

        if (tenant is null)
        {
            return request.TenantId.HasValue
                ? Result.Failure<TenantBrandingDto>(TenantErrors.NotFound(request.TenantId.Value))
                : Result.Failure<TenantBrandingDto>(
                    Error.NotFound("Tenant.NotFound", $"Tenant with slug '{request.Slug}' was not found."));
        }

        string? logoUrl = null;
        string? faviconUrl = null;

        if (tenant.LogoFileId.HasValue)
            logoUrl = await fileService.GetUrlAsync(tenant.LogoFileId.Value, cancellationToken);

        if (tenant.FaviconFileId.HasValue)
            faviconUrl = await fileService.GetUrlAsync(tenant.FaviconFileId.Value, cancellationToken);

        var dto = new TenantBrandingDto(
            tenant.Id,
            tenant.Slug,
            tenant.Status.Name,
            logoUrl,
            faviconUrl,
            tenant.PrimaryColor,
            tenant.SecondaryColor,
            tenant.LoginPageTitle,
            tenant.LoginPageSubtitle,
            tenant.Name);

        return Result.Success(dto);
    }
}
