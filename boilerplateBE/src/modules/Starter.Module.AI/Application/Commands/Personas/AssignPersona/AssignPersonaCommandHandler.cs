using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.AssignPersona;

internal sealed class AssignPersonaCommandHandler(
    AiDbContext db,
    IApplicationDbContext appDb,
    ICurrentUserService currentUser)
    : IRequestHandler<AssignPersonaCommand, Result>
{
    public async Task<Result> Handle(AssignPersonaCommand request, CancellationToken ct)
    {
        var persona = await db.AiPersonas
            .FirstOrDefaultAsync(p => p.Id == request.PersonaId, ct);
        if (persona is null)
            return Result.Failure(PersonaErrors.NotFound);
        if (!persona.IsActive)
            return Result.Failure(PersonaErrors.NotActive);

        var user = await appDb.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user is null)
            return Result.Failure(PersonaErrors.UserNotInTenant);
        if (user.TenantId != persona.TenantId)
            return Result.Failure(PersonaErrors.UserNotInTenant);

        var existing = await db.UserPersonas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(up => up.UserId == user.Id && up.PersonaId == persona.Id, ct);
        if (existing is not null)
            return Result.Failure(PersonaErrors.AlreadyAssigned);

        var makeDefault = request.MakeDefault;

        if (makeDefault)
        {
            var currentDefault = await db.UserPersonas.IgnoreQueryFilters()
                .Where(up => up.UserId == user.Id &&
                             up.TenantId == persona.TenantId &&
                             up.IsDefault)
                .FirstOrDefaultAsync(ct);
            currentDefault?.ClearDefault();
        }
        else
        {
            var anyDefault = await db.UserPersonas.IgnoreQueryFilters()
                .AnyAsync(up => up.UserId == user.Id &&
                                up.TenantId == persona.TenantId &&
                                up.IsDefault, ct);
            if (!anyDefault) makeDefault = true;
        }

        db.UserPersonas.Add(UserPersona.Create(
            user.Id, persona.Id, persona.TenantId!.Value,
            isDefault: makeDefault,
            assignedBy: currentUser.UserId));

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
