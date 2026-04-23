using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Api.Tests.Ai.Fakes;

/// <summary>
/// Scripted IAiProvider for unit tests. Each enqueued response is returned in order
/// on successive ChatAsync calls. Calls is incremented for every invocation.
/// Thread-safe — PointwiseReranker invokes ChatAsync in parallel.
/// </summary>
internal sealed class FakeAiProvider : IAiProvider
{
    private readonly ConcurrentQueue<Func<IReadOnlyList<AiChatMessage>, AiChatOptions, AiChatCompletion>> _responses = new();
    private readonly ConcurrentQueue<IReadOnlyList<AiChatChunk>> _streamedResponses = new();
    private readonly ConcurrentDictionary<string, AiChatCompletion> _contentMatchers = new();
    private readonly ConcurrentDictionary<string, Exception> _contentThrowers = new();
    private int _calls;
    public int Calls => _calls;
    public ConcurrentBag<(IReadOnlyList<AiChatMessage> Messages, AiChatOptions Options)> CallLog { get; } = new();
    public Exception? AlwaysFail { get; private set; }

    public void EnqueueContent(string content, int inputTokens = 10, int outputTokens = 5)
    {
        _responses.Enqueue((_, _) => new AiChatCompletion(content, null, inputTokens, outputTokens, "stop"));
    }

    public void EnqueueStreamChunks(IEnumerable<AiChatChunk> chunks)
    {
        _streamedResponses.Enqueue(chunks.ToArray());
    }

    public void EnqueueStreamedContent(string content, int inputTokens = 10, int outputTokens = 5)
    {
        EnqueueStreamChunks(new[]
        {
            new AiChatChunk(ContentDelta: content, ToolCallDelta: null, FinishReason: null),
            new AiChatChunk(ContentDelta: null, ToolCallDelta: null, FinishReason: "stop",
                InputTokens: inputTokens, OutputTokens: outputTokens)
        });
    }

    public void EnqueueToolCall(string name, string argsJson, int inputTokens = 10, int outputTokens = 5)
    {
        var id = Guid.NewGuid().ToString();
        _responses.Enqueue((_, _) => new AiChatCompletion(
            Content: null,
            ToolCalls: new[] { new AiToolCall(id, name, argsJson) },
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            FinishReason: "tool_calls"));
    }

    /// <summary>
    /// Content-aware scripted response: whenever any user message contains <paramref name="whenUserContains"/>,
    /// this response is returned. Intended for parallel scenarios (e.g. PointwiseReranker) where ordering
    /// of ChatAsync calls is not deterministic. Content-matched responses are checked BEFORE the queue.
    /// </summary>
    public void WhenUserContains(string whenUserContains, string content, int inputTokens = 10, int outputTokens = 5)
    {
        _contentMatchers[whenUserContains] = new AiChatCompletion(content, null, inputTokens, outputTokens, "stop");
    }

    /// <summary>
    /// Content-aware scripted exception: thrown whenever a user message contains this substring.
    /// </summary>
    public void WhenUserContainsThrow(string whenUserContains, Exception ex)
    {
        _contentThrowers[whenUserContains] = ex;
    }

    public void EnqueueThrow(Exception ex)
    {
        _responses.Enqueue((_, _) => throw ex);
    }

    public void EnqueueAllFail(string reason)
    {
        while (_responses.TryDequeue(out _)) { }
        AlwaysFail = new InvalidOperationException(reason);
    }

    public Task<AiChatCompletion> ChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct = default)
    {
        Interlocked.Increment(ref _calls);
        CallLog.Add((messages, options));
        if (AlwaysFail is not null) throw AlwaysFail;

        if (_contentMatchers.Count > 0 || _contentThrowers.Count > 0)
        {
            var user = string.Join(' ', messages.Where(m => m.Role == "user").Select(m => m.Content));
            foreach (var kvp in _contentThrowers)
            {
                if (user.Contains(kvp.Key, StringComparison.Ordinal))
                    throw kvp.Value;
            }
            foreach (var kvp in _contentMatchers)
            {
                if (user.Contains(kvp.Key, StringComparison.Ordinal))
                    return Task.FromResult(kvp.Value);
            }
        }

        if (!_responses.TryDequeue(out var factory))
            throw new InvalidOperationException("FakeAiProvider: no scripted response available.");
        return Task.FromResult(factory(messages, options));
    }

    public async IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        Interlocked.Increment(ref _calls);
        CallLog.Add((messages, options));
        if (AlwaysFail is not null) throw AlwaysFail;

        if (!_streamedResponses.TryDequeue(out var chunks))
            throw new InvalidOperationException("FakeAiProvider: no scripted stream available.");

        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            yield return chunk;
            await Task.Yield();
        }
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<float>());

    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => Task.FromResult(texts.Select(_ => Array.Empty<float>()).ToArray());
}

internal sealed class FakeAiProviderFactory : IAiProviderFactory
{
    public IAiProvider Provider { get; }
    public string EmbeddingModelId { get; set; } = "OpenAI:text-embedding-3-small";
    public string DefaultChatModelId { get; set; } = "OpenAI:gpt-4o-mini";

    public FakeAiProviderFactory(IAiProvider provider) { Provider = provider; }

    public IAiProvider Create(AiProviderType providerType) => Provider;
    public AiProviderType GetDefaultProviderType() => AiProviderType.OpenAI;
    public AiProviderType GetEmbeddingProviderType() => AiProviderType.OpenAI;
    public IAiProvider CreateDefault() => Provider;
    public IAiProvider CreateForEmbeddings() => Provider;
    public string GetEmbeddingModelId() => EmbeddingModelId;
    public string GetDefaultChatModelId() => DefaultChatModelId;
}
