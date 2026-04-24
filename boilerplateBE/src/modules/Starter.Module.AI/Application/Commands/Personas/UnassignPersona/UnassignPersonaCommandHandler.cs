using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.UnassignPersona;

internal sealed class UnassignPersonaCommandHandler(AiDbContext db)
    : IRequestHandler<UnassignPersonaCommand, Result>
{
    public async Task<Result> Handle(UnassignPersonaCommand request, CancellationToken ct)
    {
        var target = await db.UserPersonas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(up => up.UserId == request.UserId && up.PersonaId == request.PersonaId, ct);
        if (target is null)
            return Result.Failure(PersonaErrors.NotFound);

        var userAssignments = await db.UserPersonas.IgnoreQueryFilters()
            .Where(up => up.UserId == request.UserId && up.TenantId == target.TenantId)
            .ToListAsync(ct);

        if (userAssignments.Count == 1)
            return Result.Failure(PersonaErrors.CannotRemoveLastAssignment);

        if (target.IsDefault)
        {
            var promote = userAssignments
                .Where(up => up.PersonaId != target.PersonaId)
                .OrderByDescending(up => up.AssignedAt)
                .First();
            promote.MakeDefault();
        }

        db.UserPersonas.Remove(target);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
