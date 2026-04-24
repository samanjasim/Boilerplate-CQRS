using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Personas.GetPersonaById;

internal sealed class GetPersonaByIdQueryHandler(AiDbContext db)
    : IRequestHandler<GetPersonaByIdQuery, Result<AiPersonaDto>>
{
    public async Task<Result<AiPersonaDto>> Handle(GetPersonaByIdQuery request, CancellationToken ct)
    {
        var p = await db.AiPersonas.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        return p is null
            ? Result.Failure<AiPersonaDto>(PersonaErrors.NotFound)
            : Result.Success(p.ToDto());
    }
}
