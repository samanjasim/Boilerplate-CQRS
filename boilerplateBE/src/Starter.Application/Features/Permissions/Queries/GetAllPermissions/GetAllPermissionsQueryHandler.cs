using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Roles.DTOs;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Permissions.Queries.GetAllPermissions;

internal sealed class GetAllPermissionsQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetAllPermissionsQuery, Result<IReadOnlyList<PermissionGroupDto>>>
{
    public async Task<Result<IReadOnlyList<PermissionGroupDto>>> Handle(
        GetAllPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var permissions = await context.Permissions
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var grouped = permissions.ToGroupedDtoList();

        return Result.Success(grouped);
    }
}
