using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetMePersonas;

internal sealed class GetMePersonasQueryHandler(
    AiDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMePersonasQuery, Result<MePersonasDto>>
{
    public async Task<Result<MePersonasDto>> Handle(GetMePersonasQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<MePersonasDto>(AiErrors.NotAuthenticated);

        var rows = await db.UserPersonas.AsNoTracking()
            .Include(up => up.Persona)
            .Where(up => up.UserId == userId && up.Persona.IsActive)
            .OrderByDescending(up => up.IsDefault)
            .ThenBy(up => up.Persona.DisplayName)
            .ToListAsync(ct);

        var dtos = rows.Select(up => new UserPersonaDto(
            UserId: userId,
            UserDisplayName: null,
            PersonaId: up.Persona.Id,
            PersonaSlug: up.Persona.Slug,
            PersonaDisplayName: up.Persona.DisplayName,
            IsDefault: up.IsDefault,
            AssignedAt: up.AssignedAt)).ToList();

        var defaultId = rows.FirstOrDefault(r => r.IsDefault)?.PersonaId;
        return Result.Success(new MePersonasDto(dtos, defaultId));
    }
}
