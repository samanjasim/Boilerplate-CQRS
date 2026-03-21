using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Enums;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Commands.SuspendTenant;

internal sealed class SuspendTenantCommandHandler(
    IApplicationDbContext context) : IRequestHandler<SuspendTenantCommand, Result>
{
    public async Task<Result> Handle(SuspendTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure(TenantErrors.NotFound(request.Id));

        if (tenant.Status == TenantStatus.Suspended)
            return Result.Failure(TenantErrors.AlreadySuspended());

        tenant.Suspend();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
