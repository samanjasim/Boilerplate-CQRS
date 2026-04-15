using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Models;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetAssistants;

internal sealed class GetAssistantsQueryHandler(AiDbContext context)
    : IRequestHandler<GetAssistantsQuery, Result<PaginatedList<AiAssistantDto>>>
{
    public async Task<Result<PaginatedList<AiAssistantDto>>> Handle(
        GetAssistantsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.AiAssistants.AsNoTracking().AsQueryable();

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

        var page = await PaginatedList<Starter.Module.AI.Domain.Entities.AiAssistant>.CreateAsync(
            query, request.PageNumber, request.PageSize, cancellationToken);

        var dtos = page.Map(a => a.ToDto());
        return Result.Success(dtos);
    }
}
