using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.SearchKnowledgeBase;

public sealed record SearchKnowledgeBaseQuery(
    string Query,
    IReadOnlyList<Guid>? DocumentIds,
    int? TopK,
    decimal? MinScore,
    bool IncludeParents = true
) : IRequest<Result<SearchKnowledgeBaseResultDto>>;
