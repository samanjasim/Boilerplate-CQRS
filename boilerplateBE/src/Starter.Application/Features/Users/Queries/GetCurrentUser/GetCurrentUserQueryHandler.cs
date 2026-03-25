using Starter.Application.Common.Extensions;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Auth.DTOs;
using Starter.Application.Features.Users.DTOs;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Users.Queries.GetCurrentUser;

internal sealed class GetCurrentUserQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<GetCurrentUserQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is null)
            return Result.Failure<UserDto>(Error.Unauthorized());

        var user = await context.Users
            .AsNoTracking()
            .WithRolesAndPermissions()
            .FirstOrDefaultAsync(u => u.Id == currentUserService.UserId.Value, cancellationToken);

        if (user is null)
            return Result.Failure<UserDto>(UserErrors.NotFound(currentUserService.UserId.Value));

        string? tenantSlug = null;
        if (user.TenantId.HasValue)
        {
            tenantSlug = await context.Tenants
                .AsNoTracking()
                .Where(t => t.Id == user.TenantId.Value)
                .Select(t => t.Slug)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Result.Success(user.ToDto(includePermissions: true, tenantSlug: tenantSlug));
    }
}
