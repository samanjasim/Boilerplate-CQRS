using System.Runtime.CompilerServices;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Access.DTOs;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Runtime;
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

    [Fact]
    public async Task Cancelled_Token_Does_Not_Persist_Final_Row_Or_Publish_Completion_Webhook()
    {
        var recording = new RecordingWebhookPublisher();
        var fx = new ChatExecutionTestFixture(webhookPublisher: recording);
        var assistant = fx.SeedAssistantWithRagScope(AiRagScope.None);
        fx.FakeProvider.ScriptedResponse = "some reply"; // provider won't be reached with pre-cancelled ct

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => fx.RunOneTurnAsync(assistant, userMessage: "hi", ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        // No messages of any kind should be in the DB — FailTurnAsync detaches both the
        // pending user message and the new conversation (which was never saved) before throwing.
        var allMessages = await fx.GetAllMessagesAsync();
        allMessages.Where(m => m.Role == MessageRole.Assistant).Should().BeEmpty(
            "a cancelled turn must not persist an assistant reply row");

        // No ai.chat.completed webhook should have been published for the cancelled turn.
        recording.Events.Should().NotContain(
            e => e.EventType == "ai.chat.completed",
            "completing a cancelled turn would misrepresent usage");
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

    public ChatExecutionTestFixture(
        IWebhookPublisher? webhookPublisher = null,
        bool throwOnRetrieve = false,
        ILogger<ChatExecutionService>? chatLogger = null)
    {
        _retrieval.ThrowOnRetrieve = throwOnRetrieve;

        var services = new ServiceCollection();

        var dbName = $"ai-chat-{Guid.NewGuid():N}";
        services.AddDbContext<AiDbContext>(o => o.UseInMemoryDatabase(dbName));
        if (chatLogger is not null)
            services.AddSingleton(chatLogger);
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddSingleton<ICurrentUserService>(new StubCurrentUser(TenantId, UserId));
        services.AddSingleton<IQuotaChecker, StubQuotaChecker>();
        services.AddSingleton<IUsageTracker, StubUsageTracker>();
        services.AddSingleton<IWebhookPublisher>(webhookPublisher ?? new StubWebhookPublisher());
        services.AddSingleton<IAiToolRegistry, StubAiToolRegistry>();
        services.AddSingleton<IRagRetrievalService>(_retrieval);
        services.AddSingleton<ISender, NullSender>();

        services.AddSingleton<IAiProviderFactory>(new ScriptedProviderFactory(FakeProvider));
        services.AddSingleton<IResourceAccessService>(new StubResourceAccessService());

        // Agent runtime factory (Task 10 — ChatExecutionService.ExecuteAsync now delegates here)
        services.AddScoped<IAgentToolDispatcher, AgentToolDispatcher>();
        services.AddScoped<AnthropicAgentRuntime>();
        services.AddScoped<OpenAiAgentRuntime>();
        services.AddScoped<OllamaAgentRuntime>();
        services.AddScoped<IAiAgentRuntimeFactory, AiAgentRuntimeFactory>();

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
            createdByUserId: Guid.NewGuid(),
            provider: AiProviderType.Anthropic,
            model: "claude-sonnet-4",
            maxAgentSteps: 1);

        if (scope == AiRagScope.SelectedDocuments)
        {
            a.SetKnowledgeBase(docIds ?? Array.Empty<Guid>());
            a.SetRagScope(AiRagScope.SelectedDocuments);
        }
        else if (scope == AiRagScope.AllTenantDocuments)
        {
            a.SetRagScope(AiRagScope.AllTenantDocuments);
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
        AiAssistant assistant, string userMessage, CancellationToken ct = default) =>
        _chat.ExecuteAsync(conversationId: null, assistantId: assistant.Id, userMessage, ct);

    public Task<List<AiMessage>> GetAllMessagesAsync() =>
        Db.AiMessages.IgnoreQueryFilters().AsNoTracking().ToListAsync();

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
    public bool ThrowOnRetrieve { get; set; }
    public int CallCount { get; private set; }

    public Task<RetrievedContext> RetrieveForTurnAsync(
        AiAssistant assistant, string latestUserMessage, IReadOnlyList<RagHistoryMessage> history, CancellationToken ct)
    {
        _ = history;
        CallCount++;
        if (ThrowOnRetrieve) throw new InvalidOperationException("simulated retrieval failure");
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

internal sealed class RecordingWebhookPublisher : IWebhookPublisher
{
    public int DelayMs { get; set; }
    public bool ThrowOnPublish { get; set; }

    private readonly List<RecordedEvent> _events = [];
    public IReadOnlyList<RecordedEvent> Events => _events;

    public async Task PublishAsync(string eventType, Guid? tenantId, object data, CancellationToken cancellationToken = default)
    {
        if (ThrowOnPublish)
            throw new InvalidOperationException("recording publisher: simulated failure");
        if (DelayMs > 0)
            await Task.Delay(DelayMs, cancellationToken).ConfigureAwait(false);
        _events.Add(new RecordedEvent(eventType, tenantId, data));
    }
}

internal sealed record RecordedEvent(string EventType, Guid? TenantId, object Data);

internal sealed class StubResourceAccessService : IResourceAccessService
{
    public Task<Guid> GrantAsync(string resourceType, Guid resourceId, GrantSubjectType subjectType, Guid subjectId, AccessLevel level, CancellationToken ct) =>
        Task.FromResult(Guid.NewGuid());

    public Task RevokeAsync(Guid grantId, CancellationToken ct) => Task.CompletedTask;

    public Task RevokeAllForResourceAsync(string resourceType, Guid resourceId, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<ResourceGrantDto>> ListGrantsAsync(string resourceType, Guid resourceId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ResourceGrantDto>>([]);

    public Task<bool> CanAccessAsync(ICurrentUserService user, string resourceType, Guid resourceId, AccessLevel minLevel, CancellationToken ct) =>
        Task.FromResult(true);

    public Task<AccessResolution> ResolveAccessibleResourcesAsync(ICurrentUserService user, string resourceType, CancellationToken ct) =>
        Task.FromResult(new AccessResolution(IsAdminBypass: true, ExplicitGrantedResourceIds: []));

    public Task InvalidateUserAsync(Guid userId, CancellationToken ct) => Task.CompletedTask;

    public Task InvalidateRoleMembersAsync(Guid roleId, CancellationToken ct) => Task.CompletedTask;
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

internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];
    public IReadOnlyList<LogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }
}

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
