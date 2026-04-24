using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Services.Personas;

internal sealed class PersonaResolver(
    AiDbContext db,
    ICurrentUserService currentUser) : IPersonaResolver
{
    public async Task<Result<PersonaContext>> ResolveAsync(
        Guid? explicitPersonaId,
        CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var tenantId = currentUser.TenantId;

        if (explicitPersonaId.HasValue)
        {
            var persona = await db.AiPersonas
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == explicitPersonaId.Value, ct);

            if (persona is null)
                return Result.Failure<PersonaContext>(PersonaErrors.NotFound);
            if (!persona.IsActive)
                return Result.Failure<PersonaContext>(PersonaErrors.NotActive);

            if (userId.HasValue)
            {
                var assigned = await db.UserPersonas
                    .AsNoTracking()
                    .AnyAsync(up => up.UserId == userId && up.PersonaId == persona.Id, ct);
                if (!assigned)
                    return Result.Failure<PersonaContext>(PersonaErrors.NotAssignedToUser);
            }
            else if (persona.AudienceType != PersonaAudienceType.Anonymous)
            {
                return Result.Failure<PersonaContext>(PersonaErrors.RequiresAuthentication);
            }

            return Result.Success(Map(persona));
        }

        if (userId.HasValue)
        {
            var def = await db.UserPersonas
                .AsNoTracking()
                .Include(up => up.Persona)
                .Where(up => up.UserId == userId && up.IsDefault && up.Persona.IsActive)
                .Select(up => up.Persona)
                .FirstOrDefaultAsync(ct);

            if (def is null)
                return Result.Failure<PersonaContext>(PersonaErrors.NoDefaultForUser);

            return Result.Success(Map(def));
        }

        var anon = await db.AiPersonas
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.Slug == AiPersona.AnonymousSlug &&
                p.IsActive, ct);

        if (anon is null)
            return Result.Failure<PersonaContext>(PersonaErrors.AnonymousNotAvailable);

        return Result.Success(Map(anon));
    }

    private static PersonaContext Map(AiPersona p) =>
        new(p.Id, p.Slug, p.AudienceType, p.SafetyPreset, p.PermittedAgentSlugs);
}
