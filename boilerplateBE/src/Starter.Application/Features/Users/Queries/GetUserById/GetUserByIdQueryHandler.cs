using Starter.Application.Common.Extensions;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Auth.DTOs;
using Starter.Application.Features.Users.DTOs;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Users.Queries.GetUserById;

internal sealed class GetUserByIdQueryHandler(
    IApplicationDbContext context) : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .WithRolesAndPermissions()
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
            return Result.Failure<UserDto>(UserErrors.NotFound(request.Id));

        return Result.Success(user.ToDto());
    }
}
