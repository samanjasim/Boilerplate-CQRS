using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Providers;

namespace Starter.Api.Tests.Ai.Fakes;

/// <summary>
/// Scripted IAiProvider for unit tests. Each enqueued response is returned in order
/// on successive ChatAsync calls. Calls is incremented for every invocation.
/// </summary>
internal sealed class FakeAiProvider : IAiProvider
{
    private readonly Queue<Func<IReadOnlyList<AiChatMessage>, AiChatOptions, AiChatCompletion>> _responses = new();
    public int Calls { get; private set; }
    public List<(IReadOnlyList<AiChatMessage> Messages, AiChatOptions Options)> CallLog { get; } = new();
    public Exception? AlwaysFail { get; private set; }

    public void EnqueueContent(string content, int inputTokens = 10, int outputTokens = 5)
    {
        _responses.Enqueue((_, _) => new AiChatCompletion(content, null, inputTokens, outputTokens, "stop"));
    }

    public void EnqueueThrow(Exception ex)
    {
        _responses.Enqueue((_, _) => throw ex);
    }

    public void EnqueueAllFail(string reason)
    {
        _responses.Clear();
        AlwaysFail = new InvalidOperationException(reason);
    }

    public Task<AiChatCompletion> ChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct = default)
    {
        Calls++;
        CallLog.Add((messages, options));
        if (AlwaysFail is not null) throw AlwaysFail;
        if (_responses.Count == 0)
            throw new InvalidOperationException("FakeAiProvider: no scripted response available.");
        var factory = _responses.Dequeue();
        return Task.FromResult(factory(messages, options));
    }

    public IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct = default)
        => throw new NotImplementedException("FakeAiProvider.StreamChatAsync not implemented.");

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
