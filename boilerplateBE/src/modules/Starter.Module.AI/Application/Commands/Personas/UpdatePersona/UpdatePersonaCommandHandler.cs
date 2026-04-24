using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.UpdatePersona;

internal sealed class UpdatePersonaCommandHandler(AiDbContext db)
    : IRequestHandler<UpdatePersonaCommand, Result<AiPersonaDto>>
{
    public async Task<Result<AiPersonaDto>> Handle(
        UpdatePersonaCommand request,
        CancellationToken cancellationToken)
    {
        var persona = await db.AiPersonas
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (persona is null)
            return Result.Failure<AiPersonaDto>(PersonaErrors.NotFound);

        persona.Update(
            displayName: request.DisplayName,
            description: request.Description,
            safetyPreset: request.SafetyPreset,
            permittedAgentSlugs: request.PermittedAgentSlugs,
            isActive: request.IsActive);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(persona.ToDto());
    }
}
