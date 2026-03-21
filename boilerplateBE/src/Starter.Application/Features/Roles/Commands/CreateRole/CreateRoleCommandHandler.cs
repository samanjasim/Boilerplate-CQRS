using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Commands.CreateRole;

internal sealed class CreateRoleCommandHandler(
    IApplicationDbContext context) : IRequestHandler<CreateRoleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var nameExists = await context.Roles
            .AnyAsync(r => r.Name == request.Name.Trim(), cancellationToken);

        if (nameExists)
            return Result.Failure<Guid>(RoleErrors.NameAlreadyExists(request.Name));

        var role = Role.Create(
            request.Name.Trim(),
            request.Description?.Trim());

        context.Roles.Add(role);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(role.Id);
    }
}
