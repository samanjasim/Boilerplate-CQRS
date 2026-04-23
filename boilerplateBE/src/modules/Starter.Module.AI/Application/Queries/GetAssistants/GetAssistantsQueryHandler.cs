using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Paging;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Extensions;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAssistants;

internal sealed class GetAssistantsQueryHandler(
    AiDbContext context,
    IResourceAccessService access,
    ICurrentUserService currentUser)
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

        var page = await query.ToPaginatedListAsync(
            request.PageNumber, request.PageSize, cancellationToken);

        var dtos = page.Map(a => a.ToDto());
        return Result.Success(dtos);
    }
}
