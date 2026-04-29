namespace Starter.Module.AI.Infrastructure.Providers;

internal interface IAiProvider
{
    Task<AiChatCompletion> ChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct = default);

    IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct = default);

    Task<float[]> EmbedAsync(
        string text,
        CancellationToken ct = default,
        AiEmbeddingOptions? options = null);

    Task<float[][]> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default,
        AiEmbeddingOptions? options = null);
}
