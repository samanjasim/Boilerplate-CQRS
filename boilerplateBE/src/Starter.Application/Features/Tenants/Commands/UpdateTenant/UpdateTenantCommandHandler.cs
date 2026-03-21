using Starter.Application.Common.Interfaces;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Commands.UpdateTenant;

internal sealed class UpdateTenantCommandHandler(
    IApplicationDbContext context) : IRequestHandler<UpdateTenantCommand, Result>
{
    public async Task<Result> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
            return Result.Failure(TenantErrors.NotFound(request.Id));

        var nameExists = await context.Tenants
            .AnyAsync(t => t.Name == request.Name.Trim() && t.Id != request.Id, cancellationToken);

        if (nameExists)
            return Result.Failure(TenantErrors.NameAlreadyExists(request.Name));

        if (request.Slug is not null)
        {
            var slugExists = await context.Tenants
                .AnyAsync(t => t.Slug == request.Slug.Trim() && t.Id != request.Id, cancellationToken);

            if (slugExists)
                return Result.Failure(TenantErrors.SlugAlreadyExists(request.Slug));
        }

        tenant.Update(request.Name.Trim(), request.Slug?.Trim());

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
