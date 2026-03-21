using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Enums;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Commands.DeactivateTenant;

internal sealed class DeactivateTenantCommandHandler(
    IApplicationDbContext context) : IRequestHandler<DeactivateTenantCommand, Result>
{
    public async Task<Result> Handle(DeactivateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure(TenantErrors.NotFound(request.Id));

        if (tenant.Status == TenantStatus.Inactive)
            return Result.Failure(TenantErrors.AlreadyInactive());

        tenant.Deactivate();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
