using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetConversations;

internal sealed class GetConversationsQueryHandler(
    AiDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<GetConversationsQuery, Result<PaginatedList<AiConversationDto>>>
{
    public async Task<Result<PaginatedList<AiConversationDto>>> Handle(
        GetConversationsQuery request, CancellationToken cancellationToken)
    {
        // Users only see their own conversations. Tenant filter is enforced by EF query filter.
        var query = context.AiConversations.AsNoTracking().AsQueryable();

        if (currentUser.UserId is Guid userId)
            query = query.Where(c => c.UserId == userId);

        if (request.AssistantId.HasValue)
            query = query.Where(c => c.AssistantId == request.AssistantId.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(c => c.Title != null && c.Title.ToLower().Contains(term));
        }

        query = query.OrderByDescending(c => c.LastMessageAt);

        var page = await PaginatedList<AiConversation>.CreateAsync(
            query, request.PageNumber, request.PageSize, cancellationToken);

        var assistantIds = page.Items.Select(c => c.AssistantId).Distinct().ToList();
        var assistantNames = await context.AiAssistants
            .AsNoTracking()
            .Where(a => assistantIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        var result = page.Map(c => c.ToDto(
            assistantNames.TryGetValue(c.AssistantId, out var name) ? name : null));

        return Result.Success(result);
    }
}
