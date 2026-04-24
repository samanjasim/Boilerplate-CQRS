using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.SetUserDefaultPersona;

internal sealed class SetUserDefaultPersonaCommandHandler(AiDbContext db)
    : IRequestHandler<SetUserDefaultPersonaCommand, Result>
{
    public async Task<Result> Handle(SetUserDefaultPersonaCommand request, CancellationToken ct)
    {
        // Tenant query filter enforces scope — callers from other tenants see NotAssignedToUser,
        // preventing cross-tenant default flipping.
        var target = await db.UserPersonas
            .FirstOrDefaultAsync(up =>
                up.UserId == request.UserId && up.PersonaId == request.PersonaId, ct);
        if (target is null)
            return Result.Failure(PersonaErrors.NotAssignedToUser);

        var others = await db.UserPersonas
            .Where(up => up.UserId == request.UserId && up.TenantId == target.TenantId && up.IsDefault)
            .ToListAsync(ct);
        foreach (var o in others) o.ClearDefault();

        target.MakeDefault();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
