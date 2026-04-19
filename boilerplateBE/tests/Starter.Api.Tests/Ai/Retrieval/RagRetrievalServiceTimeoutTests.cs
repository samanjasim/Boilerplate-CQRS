using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Ingestion;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class RagRetrievalServiceTimeoutTests
{
    private static AiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"rag-timeout-{Guid.NewGuid():N}").Options;
        return new AiDbContext(options, currentUserService: null);
    }

    private sealed class SlowVectorStore : IVectorStore
    {
        public Task EnsureCollectionAsync(Guid t, int vs, CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(Guid t, IReadOnlyList<VectorPoint> p, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteByDocumentAsync(Guid t, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task DropCollectionAsync(Guid t, CancellationToken ct) => Task.CompletedTask;
        public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
            Guid t, float[] q, IReadOnlyCollection<Guid>? filter, int limit, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return [];
        }
    }

    private sealed class FakeKw : IKeywordSearchService
    {
        public Task<IReadOnlyList<KeywordSearchHit>> SearchAsync(
            Guid t, string q, IReadOnlyCollection<Guid>? f, int l, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<KeywordSearchHit>>([]);
    }

    private sealed class FakeEmbed : IEmbeddingService
    {
        public int VectorSize => 1536;
        public Task<float[][]> EmbedAsync(
            IReadOnlyList<string> texts, CancellationToken ct,
            EmbedAttribution? a = null, AiRequestType r = AiRequestType.Embedding)
            => Task.FromResult(texts.Select(_ => new float[1536]).ToArray());
    }

    private sealed class NoOpQueryRewriter : IQueryRewriter
    {
        public Task<IReadOnlyList<string>> RewriteAsync(string q, string? lang, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(new[] { q });
    }

    [Fact]
    public async Task VectorSearch_Timeout_ReturnsKeywordOnly_WithVectorStageDegraded()
    {
        await using var db = CreateDb();
        var settings = new AiRagSettings { StageTimeoutVectorMs = 50 };
        var svc = new RagRetrievalService(
            db,
            new SlowVectorStore(),
            new FakeKw(),
            new FakeEmbed(),
            new NoOpQueryRewriter(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        var ctx = await svc.RetrieveForTurnAsync(assistant, "query", CancellationToken.None);

        ctx.DegradedStages.Should().Contain("vector-search[0]");
    }

    // NOTE: We do not assert total latency here — CI variance makes that flaky.

    [Fact]
    public async Task Caller_Cancellation_Propagates_Not_Degraded()
    {
        await using var db = CreateDb();
        // Give the stage a generous budget — we want caller-cancel to win, not stage timeout.
        var settings = new AiRagSettings { StageTimeoutVectorMs = 60_000 };
        var svc = new RagRetrievalService(
            db,
            new SlowVectorStore(),
            new FakeKw(),
            new FakeEmbed(),
            new NoOpQueryRewriter(),
            new TokenCounter(),
            Options.Create(settings),
            NullLogger<RagRetrievalService>.Instance);

        var tenantId = Guid.NewGuid();
        var assistant = AiAssistant.Create(tenantId, "A", null, "p");
        assistant.SetRagScope(AiRagScope.AllTenantDocuments);

        using var callerCts = new CancellationTokenSource();
        callerCts.CancelAfter(TimeSpan.FromMilliseconds(50));

        Func<Task> act = () => svc.RetrieveForTurnAsync(assistant, "query", callerCts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
