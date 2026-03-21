using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Enums;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Commands.ActivateTenant;

internal sealed class ActivateTenantCommandHandler(
    IApplicationDbContext context) : IRequestHandler<ActivateTenantCommand, Result>
{
    public async Task<Result> Handle(ActivateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure(TenantErrors.NotFound(request.Id));

        if (tenant.Status == TenantStatus.Active)
            return Result.Failure(TenantErrors.AlreadyActive());

        tenant.Activate();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
