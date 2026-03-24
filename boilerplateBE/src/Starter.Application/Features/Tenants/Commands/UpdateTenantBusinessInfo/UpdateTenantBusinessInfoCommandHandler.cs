using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Commands.UpdateTenantBusinessInfo;

internal sealed class UpdateTenantBusinessInfoCommandHandler(
    IApplicationDbContext context) : IRequestHandler<UpdateTenantBusinessInfoCommand, Result>
{
    public async Task<Result> Handle(UpdateTenantBusinessInfoCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure(TenantErrors.NotFound(request.Id));

        tenant.UpdateBusinessInfo(
            request.Address,
            request.Phone,
            request.Website,
            request.TaxId);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
