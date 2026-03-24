using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Commands.UpdateTenantCustomText;

internal sealed class UpdateTenantCustomTextCommandHandler(
    IApplicationDbContext context) : IRequestHandler<UpdateTenantCustomTextCommand, Result>
{
    public async Task<Result> Handle(UpdateTenantCustomTextCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure(TenantErrors.NotFound(request.Id));

        tenant.UpdateCustomText(
            request.LoginPageTitle,
            request.LoginPageSubtitle,
            request.EmailFooterText);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
