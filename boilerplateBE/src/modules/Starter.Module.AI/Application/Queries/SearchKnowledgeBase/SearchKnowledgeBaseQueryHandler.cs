using MediatR;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Settings;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.SearchKnowledgeBase;

internal sealed class SearchKnowledgeBaseQueryHandler(
    IRagRetrievalService retrieval,
    ICurrentUserService currentUser,
    IOptions<AiRagSettings> settings)
    : IRequestHandler<SearchKnowledgeBaseQuery, Result<SearchKnowledgeBaseResultDto>>
{
    private readonly AiRagSettings _settings = settings.Value;

    public async Task<Result<SearchKnowledgeBaseResultDto>> Handle(
        SearchKnowledgeBaseQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not Guid tenantId)
            return Result.Failure<SearchKnowledgeBaseResultDto>(AiErrors.SearchRequiresTenant);

        var topK = request.TopK ?? _settings.TopK;

        var ctx = await retrieval.RetrieveForQueryAsync(
            tenantId,
            request.Query,
            request.DocumentIds is { Count: > 0 } ? request.DocumentIds.ToList() : null,
            topK,
            request.MinScore,
            request.IncludeParents,
            cancellationToken);

        var parentMap = request.IncludeParents
            ? ctx.Parents.ToDictionary(p => p.ChunkId)
            : new Dictionary<Guid, RetrievedChunk>();

        var items = new List<SearchKnowledgeBaseResultItemDto>(ctx.Children.Count * 2);
        foreach (var c in ctx.Children)
        {
            items.Add(Map(c));
            if (request.IncludeParents
                && c.ParentChunkId is Guid pid
                && parentMap.TryGetValue(pid, out var parent))
            {
                items.Add(Map(parent));
            }
        }

        return Result.Success(new SearchKnowledgeBaseResultDto(
            items,
            ctx.Children.Count,
            ctx.TruncatedByBudget));
    }

    private static SearchKnowledgeBaseResultItemDto Map(RetrievedChunk c)
    {
        var isChild = c.ChunkLevel == "child";
        return new SearchKnowledgeBaseResultItemDto(
            ChunkId: c.ChunkId,
            DocumentId: c.DocumentId,
            DocumentName: c.DocumentName,
            Content: c.Content,
            SectionTitle: c.SectionTitle,
            PageNumber: c.PageNumber,
            ChunkLevel: c.ChunkLevel,
            HybridScore: isChild ? c.HybridScore : null,
            SemanticScore: isChild ? c.SemanticScore : null,
            KeywordScore: isChild ? c.KeywordScore : null,
            ParentChunkId: c.ParentChunkId);
    }
}
