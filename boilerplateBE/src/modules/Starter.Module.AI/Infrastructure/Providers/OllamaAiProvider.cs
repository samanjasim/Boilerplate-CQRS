using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Starter.Module.AI.Infrastructure.Providers;

internal sealed class OllamaAiProvider(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<OllamaAiProvider> logger) : IAiProvider
{
    private const string DefaultChatModel = "llama3.1";
    private const string DefaultEmbeddingModel = "nomic-embed-text";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private HttpClient CreateHttpClient() => httpClientFactory.CreateClient();

    private string GetBaseUrl()
        => configuration["AI:Providers:Ollama:BaseUrl"] ?? "http://localhost:11434";

    private string ResolveChatModel(string model)
    {
        // Callers may pass the provider-prefixed identifier from IAiProviderFactory.GetDefaultChatModelId()
        // (e.g. "Ollama:llama3"). That shape is a cache key, not a wire model name —
        // strip the prefix before sending to the Ollama API.
        if (!string.IsNullOrWhiteSpace(model) && model.StartsWith("Ollama:", StringComparison.OrdinalIgnoreCase))
            model = model["Ollama:".Length..];

        if (!string.IsNullOrWhiteSpace(model) && model != DefaultChatModel)
            return model;
        return configuration["AI:Providers:Ollama:DefaultModel"] ?? DefaultChatModel;
    }

    private string ResolveEmbeddingModel(string? model = null)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            if (model.StartsWith("Ollama:", StringComparison.OrdinalIgnoreCase))
                model = model["Ollama:".Length..];

            return model;
        }

        return configuration["AI:Providers:Ollama:EmbeddingModel"] ?? DefaultEmbeddingModel;
    }

    public async Task<AiChatCompletion> ChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct = default)
    {
        var baseUrl = GetBaseUrl();
        var model = ResolveChatModel(options.Model);
        var url = $"{baseUrl}/api/chat";

        var request = BuildChatRequest(messages, options, model, stream: false);

        logger.LogDebug("Sending Ollama chat request. Model={Model}, Messages={Count}", model, messages.Count);

        using var http = CreateHttpClient();
        using var response = await http.PostAsJsonAsync(url, request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, ct)
                     ?? throw new InvalidOperationException("Empty response from Ollama /api/chat.");

        var content = result.Message?.Content;
        var finishReason = result.DoneReason ?? "stop";
        var inputTokens = result.PromptEvalCount ?? 0;
        var outputTokens = result.EvalCount ?? 0;

        return new AiChatCompletion(content, null, inputTokens, outputTokens, finishReason);
    }

    public async IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var baseUrl = GetBaseUrl();
        var model = ResolveChatModel(options.Model);
        var url = $"{baseUrl}/api/chat";

        var request = BuildChatRequest(messages, options, model, stream: true);

        logger.LogDebug("Starting Ollama streaming request. Model={Model}", model);

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json")
        };

        using var httpForStream = CreateHttpClient();
        using var httpResponse = await httpForStream.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        httpResponse.EnsureSuccessStatusCode();

        await using var stream = await httpResponse.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        string? line;
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaChatResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize Ollama streaming chunk: {Line}", line);
                continue;
            }

            if (chunk is null) continue;

            var contentDelta = chunk.Message?.Content;
            string? finishReason = chunk.Done == true ? (chunk.DoneReason ?? "stop") : null;

            if (finishReason is not null)
            {
                // Final chunk — attach prompt/eval counts for quota/usage accounting.
                yield return new AiChatChunk(
                    contentDelta,
                    null,
                    finishReason,
                    chunk.PromptEvalCount ?? 0,
                    chunk.EvalCount ?? 0);
            }
            else if (!string.IsNullOrEmpty(contentDelta))
            {
                yield return new AiChatChunk(contentDelta, null, null);
            }
        }
    }

    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken ct = default,
        AiEmbeddingOptions? options = null)
    {
        var baseUrl = GetBaseUrl();
        var model = ResolveEmbeddingModel(options?.Model);
        var url = $"{baseUrl}/api/embed";

        var request = new OllamaEmbedRequest(model, [text]);

        using var http = CreateHttpClient();
        using var response = await http.PostAsJsonAsync(url, request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(JsonOptions, ct)
                     ?? throw new InvalidOperationException("Empty response from Ollama /api/embed.");

        if (result.Embeddings is null or { Count: 0 })
            throw new InvalidOperationException("Ollama returned no embeddings.");

        return result.Embeddings[0];
    }

    public async Task<float[][]> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default,
        AiEmbeddingOptions? options = null)
    {
        // Ollama does not support batching natively — process sequentially
        var results = new float[texts.Count][];
        for (var i = 0; i < texts.Count; i++)
        {
            results[i] = await EmbedAsync(texts[i], ct, options);
        }
        return results;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static OllamaChatRequest BuildChatRequest(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        string model,
        bool stream)
    {
        var ollamaMessages = new List<OllamaMessage>(messages.Count + 1);

        if (!string.IsNullOrWhiteSpace(options.SystemPrompt))
            ollamaMessages.Add(new OllamaMessage("system", options.SystemPrompt));

        foreach (var msg in messages)
            ollamaMessages.Add(new OllamaMessage(msg.Role.ToLowerInvariant(), msg.Content ?? string.Empty));

        return new OllamaChatRequest(
            Model: model,
            Messages: ollamaMessages,
            Stream: stream,
            Options: new OllamaModelOptions(
                Temperature: options.Temperature,
                NumPredict: options.MaxTokens));
    }

    // ── Internal DTOs ─────────────────────────────────────────────────────────

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] List<OllamaMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] OllamaModelOptions? Options = null);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaModelOptions(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("num_predict")] int NumPredict);

    private sealed record OllamaChatResponse
    {
        [JsonPropertyName("model")] public string? Model { get; init; }
        [JsonPropertyName("message")] public OllamaMessage? Message { get; init; }
        [JsonPropertyName("done")] public bool? Done { get; init; }
        [JsonPropertyName("done_reason")] public string? DoneReason { get; init; }
        [JsonPropertyName("prompt_eval_count")] public int? PromptEvalCount { get; init; }
        [JsonPropertyName("eval_count")] public int? EvalCount { get; init; }
    }

    private sealed record OllamaEmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input);

    private sealed record OllamaEmbedResponse
    {
        [JsonPropertyName("model")] public string? Model { get; init; }
        [JsonPropertyName("embeddings")] public List<float[]>? Embeddings { get; init; }
    }
}
