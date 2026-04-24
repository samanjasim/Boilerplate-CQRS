using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetPersonas;

internal sealed class GetPersonasQueryHandler(AiDbContext db)
    : IRequestHandler<GetPersonasQuery, Result<IReadOnlyList<AiPersonaDto>>>
{
    public async Task<Result<IReadOnlyList<AiPersonaDto>>> Handle(
        GetPersonasQuery request, CancellationToken ct)
    {
        var q = db.AiPersonas.AsNoTracking().AsQueryable();
        if (!request.IncludeSystem) q = q.Where(p => !p.IsSystemReserved);
        if (!request.IncludeInactive) q = q.Where(p => p.IsActive);

        var rows = await q
            .OrderBy(p => p.IsSystemReserved ? 0 : 1)
            .ThenBy(p => p.DisplayName)
            .ToListAsync(ct);

        IReadOnlyList<AiPersonaDto> result = rows.Select(p => p.ToDto()).ToList();
        return Result.Success(result);
    }
}
