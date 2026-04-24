using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetPersonaAssignments;

internal sealed class GetPersonaAssignmentsQueryHandler(
    AiDbContext db,
    IApplicationDbContext appDb)
    : IRequestHandler<GetPersonaAssignmentsQuery, Result<IReadOnlyList<UserPersonaDto>>>
{
    public async Task<Result<IReadOnlyList<UserPersonaDto>>> Handle(
        GetPersonaAssignmentsQuery request, CancellationToken ct)
    {
        var persona = await db.AiPersonas.FirstOrDefaultAsync(p => p.Id == request.PersonaId, ct);
        if (persona is null)
            return Result.Failure<IReadOnlyList<UserPersonaDto>>(PersonaErrors.NotFound);

        var rows = await db.UserPersonas.AsNoTracking()
            .Where(up => up.PersonaId == persona.Id)
            .OrderBy(up => up.AssignedAt)
            .ToListAsync(ct);

        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var users = await appDb.Users.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, FirstName = u.FullName.FirstName, LastName = u.FullName.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", ct);

        IReadOnlyList<UserPersonaDto> dtos = rows.Select(up => new UserPersonaDto(
            UserId: up.UserId,
            UserDisplayName: users.TryGetValue(up.UserId, out var name) ? name : null,
            PersonaId: persona.Id,
            PersonaSlug: persona.Slug,
            PersonaDisplayName: persona.DisplayName,
            IsDefault: up.IsDefault,
            AssignedAt: up.AssignedAt)).ToList();

        return Result.Success(dtos);
    }
}
