using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Personas.CreatePersona;

internal sealed class CreatePersonaCommandHandler(
    AiDbContext db,
    ICurrentUserService currentUser,
    ISlugGenerator slugGenerator)
    : IRequestHandler<CreatePersonaCommand, Result<AiPersonaDto>>
{
    public async Task<Result<AiPersonaDto>> Handle(
        CreatePersonaCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not Guid tenantId)
            return Result.Failure<AiPersonaDto>(AiErrors.NotAuthenticated);
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<AiPersonaDto>(AiErrors.NotAuthenticated);

        var slugs = await db.AiPersonas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);
        var taken = new HashSet<string>(slugs, StringComparer.Ordinal);

        string slug;
        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            slug = request.Slug.Trim().ToLowerInvariant();
            if (taken.Contains(slug))
                return Result.Failure<AiPersonaDto>(PersonaErrors.SlugAlreadyExists(slug));
        }
        else
        {
            slug = slugGenerator.EnsureUnique(slugGenerator.Slugify(request.DisplayName), taken);
        }

        var persona = AiPersona.Create(
            tenantId: tenantId,
            slug: slug,
            displayName: request.DisplayName,
            description: request.Description,
            audienceType: request.AudienceType,
            safetyPreset: request.SafetyPreset,
            createdByUserId: userId);

        if (request.PermittedAgentSlugs is { Count: > 0 })
            persona.Update(
                request.DisplayName,
                request.Description,
                request.SafetyPreset,
                request.PermittedAgentSlugs,
                isActive: true);

        db.AiPersonas.Add(persona);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(persona.ToDto());
    }
}
