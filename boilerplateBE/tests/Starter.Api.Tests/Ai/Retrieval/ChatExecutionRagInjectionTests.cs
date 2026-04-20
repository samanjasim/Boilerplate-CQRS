using System.Runtime.CompilerServices;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class ChatExecutionRagInjectionTests
{
    [Fact]
    public async Task RagScope_None_Does_Not_Inject_Context()
    {
        var fx = new ChatExecutionTestFixture();
        var assistant = fx.SeedAssistantWithRagScope(AiRagScope.None);
        fx.FakeProvider.ScriptedResponse = "plain reply";

        var reply = await fx.RunOneTurnAsync(assistant, userMessage: "hello");

        reply.IsSuccess.Should().BeTrue();
        fx.FakeProvider.LastSystemPrompt.Should().Be(assistant.SystemPrompt);
        fx.FakeProvider.LastSystemPrompt.Should().NotContain("<context>");

        var savedMessage = await fx.LoadAssistantMessageAsync(reply.Value!.AssistantMessage.Id);
        savedMessage.Citations.Should().BeEmpty();
        fx.RetrievalCallCount.Should().Be(0);
    }

    [Fact]
    public async Task RagScope_SelectedDocuments_Injects_Context_And_Citations()
    {
        var fx = new ChatExecutionTestFixture();
        var docId = fx.SeedTwoRetrievedChunks();
        var assistant = fx.SeedAssistantWithRagScope(
            AiRagScope.SelectedDocuments, docIds: new[] { docId });

        fx.FakeProvider.ScriptedResponse = "The answer references [1] and [2].";

        var reply = await fx.RunOneTurnAsync(assistant, userMessage: "what does doc X say");

        reply.IsSuccess.Should().BeTrue();
        fx.FakeProvider.LastSystemPrompt.Should().Contain("<context>");
        fx.RetrievalCallCount.Should().Be(1);

        var savedMessage = await fx.LoadAssistantMessageAsync(reply.Value!.AssistantMessage.Id);
        savedMessage.Citations.Should().HaveCount(2);
        savedMessage.Citations.Select(c => c.Marker).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task Fallback_When_Model_Emits_No_Markers_Populates_Full_Chunk_Set()
    {
        var fx = new ChatExecutionTestFixture();
        var docId = fx.SeedTwoRetrievedChunks();
        var assistant = fx.SeedAssistantWithRagScope(
            AiRagScope.SelectedDocuments, docIds: new[] { docId });

        fx.FakeProvider.ScriptedResponse = "plain answer with no citation tags";

        var reply = await fx.RunOneTurnAsync(assistant, userMessage: "q");

        reply.IsSuccess.Should().BeTrue();
        var savedMessage = await fx.LoadAssistantMessageAsync(reply.Value!.AssistantMessage.Id);
        savedMessage.Citations.Should().HaveCount(2);
    }

    [Fact]
    public async Task Stream_Emits_Citations_Event_Before_Done()
    {
        var fx = new ChatExecutionTestFixture();
        var docId = fx.SeedTwoRetrievedChunks();
        var assistant = fx.SeedAssistantWithRagScope(
            AiRagScope.SelectedDocuments, docIds: new[] { docId });

        fx.FakeProvider.ScriptedResponse = "answer references [1]";

        var events = await fx.RunOneStreamingTurnAsync(assistant, userMessage: "q");

        var citationIdx = events.FindIndex(e => e.Type == "citations");
        var doneIdx = events.FindIndex(e => e.Type == "done");

        citationIdx.Should().BeGreaterThanOrEqualTo(0, "citations event should be emitted");
        doneIdx.Should().BeGreaterThan(citationIdx, "citations must come before done");
    }
}

// ─────────────────────────────────────────────────────────────────────────
// Fixture
// ─────────────────────────────────────────────────────────────────────────

internal sealed class ChatExecutionTestFixture
{
    public AiDbContext Db { get; }
    public ScriptedAiProvider FakeProvider { get; } = new();
    public Guid TenantId { get; } = Guid.NewGuid();
    public Guid UserId { get; } = Guid.NewGuid();
    public int RetrievalCallCount => _retrieval.CallCount;

    private readonly FakeRetrieval _retrieval = new();
    private readonly IChatExecutionService _chat;

    public ChatExecutionTestFixture()
    {
        var services = new ServiceCollection();

        var dbName = $"ai-chat-{Guid.NewGuid():N}";
        services.AddDbContext<AiDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddSingleton<ICurrentUserService>(new StubCurrentUser(TenantId, UserId));
        services.AddSingleton<IQuotaChecker, StubQuotaChecker>();
        services.AddSingleton<IUsageTracker, StubUsageTracker>();
        services.AddSingleton<IWebhookPublisher, StubWebhookPublisher>();
        services.AddSingleton<IAiToolRegistry, StubAiToolRegistry>();
        services.AddSingleton<IRagRetrievalService>(_retrieval);
        services.AddSingleton<ISender, NullSender>();

        services.AddSingleton<IAiProviderFactory>(new ScriptedProviderFactory(FakeProvider));

        services.AddScoped<IChatExecutionService, ChatExecutionService>();

        var sp = services.BuildServiceProvider();
        Db = sp.GetRequiredService<AiDbContext>();
        _chat = sp.GetRequiredService<IChatExecutionService>();
    }

    public AiAssistant SeedAssistantWithRagScope(AiRagScope scope, Guid[]? docIds = null)
    {
        var a = AiAssistant.Create(
            tenantId: TenantId,
            name: $"A-{Guid.NewGuid():N}",
            description: null,
            systemPrompt: "You are a helpful assistant.",
            provider: AiProviderType.Anthropic,
            model: "claude-sonnet-4",
            maxAgentSteps: 1);

        if (scope == AiRagScope.SelectedDocuments)
        {
            a.SetKnowledgeBase(docIds ?? Array.Empty<Guid>());
            a.SetRagScope(AiRagScope.SelectedDocuments);
        }

        Db.AiAssistants.Add(a);
        Db.SaveChanges();
        return a;
    }

    public Guid SeedTwoRetrievedChunks()
    {
        var docId = Guid.NewGuid();
        var chunks = new List<RetrievedChunk>
        {
            new(Guid.NewGuid(), docId, "doc.pdf", "first chunk content",
                SectionTitle: "S1", PageNumber: 1, ChunkLevel: "child",
                SemanticScore: 0.9m, KeywordScore: 0.5m, HybridScore: 0.8m, ParentChunkId: null, ChunkIndex: 0),
            new(Guid.NewGuid(), docId, "doc.pdf", "second chunk content",
                SectionTitle: "S2", PageNumber: 2, ChunkLevel: "child",
                SemanticScore: 0.85m, KeywordScore: 0.4m, HybridScore: 0.75m, ParentChunkId: null, ChunkIndex: 1),
        };

        _retrieval.Context = new RetrievedContext(chunks, Parents: [], TotalTokens: 20, TruncatedByBudget: false, DegradedStages: [], Siblings: [], FusedCandidates: 0, DetectedLanguage: "unknown");
        return docId;
    }

    public RetrievedContext CurrentRetrievalContext => _retrieval.Context;

    public void OverrideRetrievalContext(
        IReadOnlyList<RetrievedChunk> children,
        bool truncated,
        IReadOnlyList<string> degradedStages)
    {
        _retrieval.Context = new RetrievedContext(
            Children: children,
            Parents: [],
            TotalTokens: children.Sum(c => Math.Max(1, c.Content.Length / 4)),
            TruncatedByBudget: truncated,
            DegradedStages: degradedStages,
            Siblings: [],
            FusedCandidates: 0,
            DetectedLanguage: "unknown");
    }

    public Task<Starter.Shared.Results.Result<AiChatReplyDto>> RunOneTurnAsync(
        AiAssistant assistant, string userMessage) =>
        _chat.ExecuteAsync(conversationId: null, assistantId: assistant.Id, userMessage, CancellationToken.None);

    public async Task<List<ChatStreamEvent>> RunOneStreamingTurnAsync(
        AiAssistant assistant, string userMessage)
    {
        var events = new List<ChatStreamEvent>();
        await foreach (var ev in _chat.ExecuteStreamAsync(
            conversationId: null, assistantId: assistant.Id, userMessage, CancellationToken.None))
        {
            events.Add(ev);
        }
        return events;
    }

    public async Task<AiMessage> LoadAssistantMessageAsync(Guid messageId) =>
        await Db.AiMessages.IgnoreQueryFilters().AsNoTracking().SingleAsync(m => m.Id == messageId);
}

internal sealed class NullSender : ISender
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<object?> Send(object request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) =>
        throw new NotSupportedException();
}

// ─────────────────────────────────────────────────────────────────────────
// Fakes
// ─────────────────────────────────────────────────────────────────────────

internal sealed class ScriptedProviderFactory(ScriptedAiProvider provider) : IAiProviderFactory
{
    public IAiProvider Create(AiProviderType providerType) => provider;
    public AiProviderType GetDefaultProviderType() => AiProviderType.Anthropic;
    public AiProviderType GetEmbeddingProviderType() => AiProviderType.Anthropic;
    public IAiProvider CreateDefault() => provider;
    public IAiProvider CreateForEmbeddings() => provider;
    public string GetEmbeddingModelId() => "Anthropic:test";
    public string GetDefaultChatModelId() => "Anthropic:claude-3-5-haiku-20241022";
}

internal sealed class ScriptedAiProvider : IAiProvider
{
    public string ScriptedResponse { get; set; } = "";
    public string? LastSystemPrompt { get; private set; }

    public Task<AiChatCompletion> ChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct = default)
    {
        LastSystemPrompt = options.SystemPrompt;
        return Task.FromResult(new AiChatCompletion(
            Content: ScriptedResponse,
            ToolCalls: null,
            InputTokens: 10,
            OutputTokens: 5,
            FinishReason: "stop"));
    }

    public async IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        LastSystemPrompt = options.SystemPrompt;
        yield return new AiChatChunk(ContentDelta: ScriptedResponse, ToolCallDelta: null, FinishReason: null);
        yield return new AiChatChunk(
            ContentDelta: null, ToolCallDelta: null, FinishReason: "stop",
            InputTokens: 10, OutputTokens: 5);
        await Task.CompletedTask;
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) =>
        throw new NotSupportedException();
}

internal sealed class FakeRetrieval : IRagRetrievalService
{
    public RetrievedContext Context { get; set; } = RetrievedContext.Empty;
    public int CallCount { get; private set; }

    public Task<RetrievedContext> RetrieveForTurnAsync(
        AiAssistant assistant, string latestUserMessage, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(Context);
    }

    public Task<RetrievedContext> RetrieveForQueryAsync(
        Guid tenantId, string queryText, IReadOnlyCollection<Guid>? documentFilter,
        int topK, decimal? minScore, bool includeParents, CancellationToken ct) =>
        throw new NotSupportedException();
}

internal sealed class StubCurrentUser(Guid tenantId, Guid userId) : ICurrentUserService
{
    public Guid? TenantId { get; } = tenantId;
    public Guid? UserId { get; } = userId;
    public string? Email => null;
    public bool IsAuthenticated => true;
    public IEnumerable<string> Roles => [];
    public IEnumerable<string> Permissions => [];
    public bool IsInRole(string role) => false;
    public bool HasPermission(string permission) => false;
}

internal sealed class StubQuotaChecker : IQuotaChecker
{
    public Task<QuotaResult> CheckAsync(Guid tenantId, string metric, int increment = 1, CancellationToken ct = default) =>
        Task.FromResult(QuotaResult.Unlimited());

    public Task IncrementAsync(Guid tenantId, string metric, int amount = 1, CancellationToken ct = default) =>
        Task.CompletedTask;
}

internal sealed class StubUsageTracker : IUsageTracker
{
    public Task<long> GetAsync(Guid tenantId, string metric, CancellationToken ct = default) => Task.FromResult(0L);
    public Task IncrementAsync(Guid tenantId, string metric, long amount = 1, CancellationToken ct = default) => Task.CompletedTask;
    public Task DecrementAsync(Guid tenantId, string metric, long amount = 1, CancellationToken ct = default) => Task.CompletedTask;
    public Task SetAsync(Guid tenantId, string metric, long value, CancellationToken ct = default) => Task.CompletedTask;
    public Task<Dictionary<string, long>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(new Dictionary<string, long>());
}

internal sealed class StubWebhookPublisher : IWebhookPublisher
{
    public Task PublishAsync(string eventType, Guid? tenantId, object data, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class StubAiToolRegistry : IAiToolRegistry
{
    public Task<IReadOnlyList<AiToolDto>> ListAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AiToolDto>>([]);

    public IAiToolDefinition? FindByName(string name) => null;

    public Task<ToolResolutionResult> ResolveForAssistantAsync(AiAssistant assistant, CancellationToken ct) =>
        Task.FromResult(new ToolResolutionResult(
            ProviderTools: [],
            DefinitionsByName: new Dictionary<string, IAiToolDefinition>()));
}
