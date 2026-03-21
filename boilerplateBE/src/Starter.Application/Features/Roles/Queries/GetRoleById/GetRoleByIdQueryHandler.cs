using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Roles.DTOs;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Queries.GetRoleById;

internal sealed class GetRoleByIdQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetRoleByIdQuery, Result<RoleDto>>
{
    public async Task<Result<RoleDto>> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role is null)
            return Result.Failure<RoleDto>(RoleErrors.NotFound(request.Id));

        return Result.Success(role.ToDto());
    }
}
