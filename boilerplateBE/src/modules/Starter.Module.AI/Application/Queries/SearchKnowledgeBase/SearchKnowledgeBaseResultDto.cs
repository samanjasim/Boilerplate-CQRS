namespace Starter.Module.AI.Application.Queries.SearchKnowledgeBase;

public sealed record SearchKnowledgeBaseResultDto(
    IReadOnlyList<SearchKnowledgeBaseResultItemDto> Items,
    int TotalHits,
    bool TruncatedByBudget);
