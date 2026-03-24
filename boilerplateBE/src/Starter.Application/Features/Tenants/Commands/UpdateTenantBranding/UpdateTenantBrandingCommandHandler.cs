using Starter.Application.Common.Constants;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Commands.UpdateTenantBranding;

internal sealed class UpdateTenantBrandingCommandHandler(
    IApplicationDbContext context,
    IFileService fileService) : IRequestHandler<UpdateTenantBrandingCommand, Result>
{
    public async Task<Result> Handle(UpdateTenantBrandingCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure(TenantErrors.NotFound(request.Id));

        // Handle logo
        if (request.LogoFileId.HasValue)
        {
            await fileService.ReplaceEntityFileAsync(
                tenant.LogoFileId, request.LogoFileId.Value, tenant.Id, FileEntityTypes.Tenant, cancellationToken);
        }
        else if (request.RemoveLogo && tenant.LogoFileId.HasValue)
        {
            await fileService.DetachFromEntityAsync(tenant.LogoFileId.Value, cancellationToken);
        }

        // Handle favicon
        if (request.FaviconFileId.HasValue)
        {
            await fileService.ReplaceEntityFileAsync(
                tenant.FaviconFileId, request.FaviconFileId.Value, tenant.Id, FileEntityTypes.Tenant, cancellationToken);
        }
        else if (request.RemoveFavicon && tenant.FaviconFileId.HasValue)
        {
            await fileService.DetachFromEntityAsync(tenant.FaviconFileId.Value, cancellationToken);
        }

        tenant.UpdateBranding(
            request.LogoFileId ?? (request.RemoveLogo ? null : tenant.LogoFileId),
            request.FaviconFileId ?? (request.RemoveFavicon ? null : tenant.FaviconFileId),
            request.PrimaryColor,
            request.SecondaryColor,
            request.Description);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
