using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.DeletePersona;

internal sealed class DeletePersonaCommandHandler(AiDbContext db)
    : IRequestHandler<DeletePersonaCommand, Result>
{
    public async Task<Result> Handle(DeletePersonaCommand request, CancellationToken ct)
    {
        var persona = await db.AiPersonas.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (persona is null)
            return Result.Failure(PersonaErrors.NotFound);
        if (persona.IsSystemReserved)
            return Result.Failure(PersonaErrors.CannotDeleteSystemReserved);

        var hasAssignments = await db.UserPersonas.AnyAsync(up => up.PersonaId == persona.Id, ct);
        if (hasAssignments)
            return Result.Failure(PersonaErrors.HasActiveAssignments);

        db.AiPersonas.Remove(persona);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
