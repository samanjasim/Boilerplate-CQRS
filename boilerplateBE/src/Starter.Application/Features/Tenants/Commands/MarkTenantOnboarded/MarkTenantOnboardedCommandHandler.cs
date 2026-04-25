using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Commands.MarkTenantOnboarded;

internal sealed class MarkTenantOnboardedCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<MarkTenantOnboardedCommand, Result>
{
    public async Task<Result> Handle(MarkTenantOnboardedCommand request, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters lets SuperAdmin (TenantId=null) target any tenant.
        // Tenant-scoped callers are re-asserted to their own tenant below.
        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
            return Result.Failure(TenantErrors.NotFound(request.TenantId));

        if (currentUserService.TenantId.HasValue && tenant.Id != currentUserService.TenantId.Value)
            return Result.Failure(Error.Forbidden("You cannot modify another tenant's onboarding state."));

        if (request.Onboarded)
            tenant.MarkOnboarded(DateTimeOffset.UtcNow);
        else
            tenant.ClearOnboarded();

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
