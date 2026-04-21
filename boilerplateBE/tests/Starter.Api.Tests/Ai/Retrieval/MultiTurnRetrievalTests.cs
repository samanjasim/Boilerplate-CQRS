using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class MultiTurnRetrievalTests
{
    private static AiDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"multiturn-{Guid.NewGuid():N}")
            .Options, currentUserService: null);

    private static AiRagSettings DefaultSettings(bool enableContextualRewrite = true) =>
        new()
        {
            TopK = 5,
            RetrievalTopK = 20,
            VectorWeight = 1.0m,
            KeywordWeight = 1.0m,
            MaxContextTokens = 4000,
            IncludeParentContext = false,
            MinHybridScore = 0.0m,
            EnableContextualRewrite = enableContextualRewrite
        };

    private static RagRetrievalService Build(
        AiDbContext db,
        FakeAiProvider provider,
        KeywordAwareFakeKeywordSearchService keywordSearch,
        AiRagSettings settings)
    {
        var factory = new FakeAiProviderFactory(provider);
        var resolver = new ContextualQueryResolver(
            factory,
            new FakeCacheService(),
            Options.Create(settings),
            NullLogger<ContextualQueryResolver>.Instance);

        return new RagRetrievalService(
            db,
            new FakeVectorStore(),
            keywordSearch,
            new FakeEmbeddingService(),
            new NoOpQueryRewriter(),
            resolver,
            new NoOpQuestionClassifier(),
            new NoOpReranker(),
            new RerankStrategySelector(settings),
            new NoOpNeighborExpander(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);
    }

    private static IReadOnlyList<RagHistoryMessage> History(params (string role, string content)[] turns) =>
        turns.Select(t => new RagHistoryMessage(t.role, t.content)).ToList();

    [Fact]
    public async Task Two_turn_chat_follow_up_retrieves_resolved_concept()
    {
        await using var db = CreateDb();
        var settings = DefaultSettings(enableContextualRewrite: true);

        var provider = new FakeAiProvider();
        provider.WhenUserContains("how do we configure it?", "How do we configure Qdrant?");

        var keywordSearch = new KeywordAwareFakeKeywordSearchService();
        var svc = Build(db, provider, keywordSearch, settings);

        var assistant = AiAssistant.Create(Guid.NewGuid(), "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var history = History(
            ("user", "what is qdrant?"),
            ("assistant", "qdrant is a vector db."));

        await svc.RetrieveForTurnAsync(assistant, "how do we configure it?", history, CancellationToken.None);

        keywordSearch.LastQueryText.Should().Contain("Qdrant");
    }

    [Fact]
    public async Task Self_contained_follow_up_is_not_rewritten()
    {
        await using var db = CreateDb();
        var settings = DefaultSettings(enableContextualRewrite: true);

        var provider = new FakeAiProvider();
        var keywordSearch = new KeywordAwareFakeKeywordSearchService();
        var svc = Build(db, provider, keywordSearch, settings);

        var assistant = AiAssistant.Create(Guid.NewGuid(), "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var history = History(
            ("user", "hi"),
            ("assistant", "hello"));

        await svc.RetrieveForTurnAsync(
            assistant,
            "Tell me about MinIO object storage and bucket policies",
            history,
            CancellationToken.None);

        provider.Calls.Should().Be(0);
        keywordSearch.LastQueryText.Should().Contain("MinIO");
    }

    [Fact]
    public async Task Empty_history_preserves_pre_4b5_behavior_no_contextualize_stage()
    {
        await using var db = CreateDb();
        var settings = DefaultSettings(enableContextualRewrite: true);

        var provider = new FakeAiProvider();
        var keywordSearch = new KeywordAwareFakeKeywordSearchService();
        var svc = Build(db, provider, keywordSearch, settings);

        var assistant = AiAssistant.Create(Guid.NewGuid(), "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        await svc.RetrieveForTurnAsync(
            assistant,
            "What is Qdrant?",
            Array.Empty<RagHistoryMessage>(),
            CancellationToken.None);

        provider.Calls.Should().Be(0);
        keywordSearch.LastQueryText.Should().Contain("Qdrant");
    }

    [Fact]
    public async Task Arabic_follow_up_resolves_to_arabic_query()
    {
        await using var db = CreateDb();
        var settings = DefaultSettings(enableContextualRewrite: true);

        var provider = new FakeAiProvider();
        provider.WhenUserContains("كيف نضبطه؟", "كيف نضبط Qdrant؟");

        var keywordSearch = new KeywordAwareFakeKeywordSearchService();
        var svc = Build(db, provider, keywordSearch, settings);

        var assistant = AiAssistant.Create(Guid.NewGuid(), "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var history = History(
            ("user", "ما هو Qdrant؟"),
            ("assistant", "Qdrant هو قاعدة بيانات متجهية."));

        await svc.RetrieveForTurnAsync(
            assistant,
            "كيف نضبطه؟",
            history,
            CancellationToken.None);

        keywordSearch.LastQueryText.Should().Contain("Qdrant");
    }
}

internal sealed class KeywordAwareFakeKeywordSearchService : IKeywordSearchService
{
    public string? LastQueryText { get; private set; }

    public Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
        Guid tenantId,
        string queryText,
        IReadOnlyCollection<Guid>? documentFilter,
        int limit,
        CancellationToken ct)
    {
        LastQueryText = queryText;
        IReadOnlyList<KeywordSearchHit> result = Array.Empty<KeywordSearchHit>();
        return Task.FromResult(result);
    }
}
