using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Extensions;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAssistants;

internal sealed class GetAssistantsQueryHandler(
    AiDbContext context,
    IResourceAccessService access,
    ICurrentUserService currentUser,
    IPersonaResolver personaResolver,
    IPersonaContextAccessor personaContextAccessor,
    IConfiguration configuration)
    : IRequestHandler<GetAssistantsQuery, Result<PaginatedList<AiAssistantDto>>>
{
    public async Task<Result<PaginatedList<AiAssistantDto>>> Handle(
        GetAssistantsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.AiAssistants.AsNoTracking().AsQueryable();

        var resolution = await access.ResolveAccessibleResourcesAsync(
            currentUser, ResourceTypes.AiAssistant, cancellationToken);

        if (!resolution.IsAdminBypass)
        {
            var userId = currentUser.UserId;
            var grantedIds = resolution.ExplicitGrantedResourceIds;
            query = query.Where(a =>
                a.Visibility == ResourceVisibility.TenantWide ||
                (userId != null && a.CreatedByUserId == userId) ||
                grantedIds.Contains(a.Id));
        }

        if (request.IsActive is bool active)
            query = query.Where(a => a.IsActive == active);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.Name.ToLower().Contains(term) ||
                (a.Description != null && a.Description.ToLower().Contains(term)));
        }

        query = query.OrderByDescending(a => a.CreatedAt);

        // Plan 5b — persona visibility filter. Resolve the caller's persona here instead of
        // relying on ChatExecutionService to have populated the accessor — list endpoints
        // don't go through the chat pipeline. Feature flag Ai:Personas:Enabled (default true)
        // short-circuits persona filtering when the kill-switch is active.
        var personasEnabled = configuration.GetValue<bool?>("AI:Personas:Enabled") ?? true;
        PersonaContext? personaCtx = personaContextAccessor.Current;
        if (personasEnabled && personaCtx is null && currentUser.UserId.HasValue)
        {
            var resolved = await personaResolver.ResolveAsync(explicitPersonaId: null, cancellationToken);
            if (resolved.IsSuccess)
            {
                personaCtx = resolved.Value;
                personaContextAccessor.Set(personaCtx);
            }
        }

        if (personasEnabled && personaCtx is not null)
        {
            var materialised = await query.ToListAsync(cancellationToken);
            var visible = materialised
                .Where(a => a.IsVisibleToPersona(personaCtx.Slug, personaCtx.PermittedAgentSlugs))
                .ToList();

            var total = visible.Count;
            var pageItems = visible
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(a => a.ToDto())
                .ToList();
            return Result.Success(new PaginatedList<AiAssistantDto>(
                pageItems, total, request.PageNumber, request.PageSize));
        }

        var page = await query.ToPaginatedListAsync(
            request.PageNumber, request.PageSize, cancellationToken);

        var dtos = page.Map(a => a.ToDto());
        return Result.Success(dtos);
    }
}
