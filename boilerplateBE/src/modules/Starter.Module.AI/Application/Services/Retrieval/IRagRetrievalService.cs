using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.Services.Retrieval;

public interface IRagRetrievalService
{
    Task<RetrievedContext> RetrieveForTurnAsync(
        AiAssistant assistant,
        string latestUserMessage,
        CancellationToken ct);

    Task<RetrievedContext> RetrieveForQueryAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int topK,
        decimal? minScore,
        bool includeParents,
        CancellationToken ct);
}
