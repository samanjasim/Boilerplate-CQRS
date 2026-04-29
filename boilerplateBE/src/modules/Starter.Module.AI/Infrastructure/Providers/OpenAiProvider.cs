using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace Starter.Module.AI.Infrastructure.Providers;

internal sealed class OpenAiProvider(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<OpenAiProvider> logger) : IAiProvider
{
    private const string DefaultChatModel = "gpt-4o";
    private const string DefaultEmbeddingModel = "text-embedding-3-small";

    private string GetApiKey(string? resolvedApiKey = null)
        => !string.IsNullOrWhiteSpace(resolvedApiKey)
            ? resolvedApiKey
            : configuration["AI:Providers:OpenAI:ApiKey"]
              ?? throw new InvalidOperationException("OpenAI API key is not configured (AI:Providers:OpenAI:ApiKey).");

    // Route the OpenAI SDK's pipeline through a pooled HttpClient so socket/DNS caches
    // are shared instead of a fresh connection pool per ChatClient/EmbeddingClient instance.
    private OpenAIClientOptions BuildClientOptions() => new()
    {
        Transport = new HttpClientPipelineTransport(httpClientFactory.CreateClient(nameof(OpenAiProvider)))
    };

    private string ResolveChatModel(string model)
    {
        // Callers may pass the provider-prefixed identifier from IAiProviderFactory.GetDefaultChatModelId()
        // (e.g. "OpenAI:gpt-4o-mini"). That shape is a cache key, not a wire model name —
        // strip the prefix before sending to the OpenAI SDK.
        if (!string.IsNullOrWhiteSpace(model) && model.StartsWith("OpenAI:", StringComparison.OrdinalIgnoreCase))
            model = model["OpenAI:".Length..];

        if (!string.IsNullOrWhiteSpace(model) && model != DefaultChatModel)
            return model;
        return configuration["AI:Providers:OpenAI:DefaultModel"] ?? DefaultChatModel;
    }

    private string ResolveEmbeddingModel(string? model = null)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            if (model.StartsWith("OpenAI:", StringComparison.OrdinalIgnoreCase))
                model = model["OpenAI:".Length..];

            return model;
        }

        return configuration["AI:Providers:OpenAI:EmbeddingModel"] ?? DefaultEmbeddingModel;
    }

    public async Task<AiChatCompletion> ChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct = default)
    {
        var apiKey = GetApiKey(options.ApiKey);
        var model = ResolveChatModel(options.Model);
        var client = new ChatClient(model, new ApiKeyCredential(apiKey), BuildClientOptions());

        var chatMessages = MapMessages(messages, options.SystemPrompt);
        var completionOptions = BuildOptions(options);

        logger.LogDebug("Sending OpenAI chat request. Model={Model}, Messages={Count}", model, chatMessages.Count);

        var response = await client.CompleteChatAsync(chatMessages, completionOptions, ct);
        var completion = response.Value;

        var content = ExtractTextContent(completion.Content);
        var toolCalls = ExtractToolCalls(completion.ToolCalls);
        var inputTokens = completion.Usage?.InputTokenCount ?? 0;
        var outputTokens = completion.Usage?.OutputTokenCount ?? 0;
        var finishReason = MapFinishReason(completion.FinishReason);

        return new AiChatCompletion(content, toolCalls, inputTokens, outputTokens, finishReason);
    }

    public async IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var apiKey = GetApiKey(options.ApiKey);
        var model = ResolveChatModel(options.Model);
        var client = new ChatClient(model, new ApiKeyCredential(apiKey), BuildClientOptions());

        var chatMessages = MapMessages(messages, options.SystemPrompt);
        var completionOptions = BuildOptions(options);

        logger.LogDebug("Starting OpenAI streaming request. Model={Model}", model);

        // Track tool call accumulation per index
        var toolCallBuilders = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        string? pendingFinishReason = null;
        int inputTokens = 0;
        int outputTokens = 0;

        await foreach (var update in client.CompleteChatStreamingAsync(chatMessages, completionOptions, ct))
        {
            // Content delta
            if (update.ContentUpdate.Count > 0)
            {
                var text = string.Concat(update.ContentUpdate.Select(c => c.Text));
                if (!string.IsNullOrEmpty(text))
                    yield return new AiChatChunk(text, null, null);
            }

            // Tool call deltas
            foreach (var toolUpdate in update.ToolCallUpdates)
            {
                var idx = toolUpdate.Index;
                if (!toolCallBuilders.TryGetValue(idx, out var builder))
                {
                    builder = (toolUpdate.ToolCallId ?? string.Empty, toolUpdate.FunctionName ?? string.Empty, new StringBuilder());
                    toolCallBuilders[idx] = builder;
                }

                var argChunk = toolUpdate.FunctionArgumentsUpdate?.ToString();
                if (!string.IsNullOrEmpty(argChunk))
                    builder.Args.Append(argChunk);

                // Emit partial tool call chunk for streaming consumers
                if (!string.IsNullOrEmpty(argChunk))
                {
                    var partial = new AiToolCall(builder.Id, builder.Name, argChunk);
                    yield return new AiChatChunk(null, partial, null);
                }
            }

            // Finish reason — hold until we see usage (OpenAI sends usage in a trailing update when IncludeUsage is on).
            if (update.FinishReason.HasValue)
            {
                pendingFinishReason = MapFinishReason(update.FinishReason.Value);
            }

            // Usage arrives in the final update (SDK sets IncludeUsage=true automatically for streams).
            if (update.Usage is { } usage)
            {
                inputTokens = usage.InputTokenCount;
                outputTokens = usage.OutputTokenCount;
            }
        }

        // Flush the terminal frame with finish reason and final usage.
        if (pendingFinishReason is not null)
        {
            yield return new AiChatChunk(null, null, pendingFinishReason, inputTokens, outputTokens);
        }
    }

    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken ct = default,
        AiEmbeddingOptions? options = null)
    {
        var apiKey = GetApiKey(options?.ApiKey);
        var embeddingModel = ResolveEmbeddingModel(options?.Model);
        var client = new EmbeddingClient(embeddingModel, new ApiKeyCredential(apiKey), BuildClientOptions());

        var result = await client.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return result.Value.ToFloats().ToArray();
    }

    public async Task<float[][]> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default,
        AiEmbeddingOptions? options = null)
    {
        var apiKey = GetApiKey(options?.ApiKey);
        var embeddingModel = ResolveEmbeddingModel(options?.Model);
        var client = new EmbeddingClient(embeddingModel, new ApiKeyCredential(apiKey), BuildClientOptions());

        var result = await client.GenerateEmbeddingsAsync(texts, cancellationToken: ct);
        return result.Value.Select(e => e.ToFloats().ToArray()).ToArray();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<ChatMessage> MapMessages(IReadOnlyList<AiChatMessage> messages, string? systemPrompt)
    {
        var result = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
            result.Add(ChatMessage.CreateSystemMessage(systemPrompt));

        foreach (var msg in messages)
        {
            switch (msg.Role.ToLowerInvariant())
            {
                case "system":
                    result.Add(ChatMessage.CreateSystemMessage(msg.Content ?? string.Empty));
                    break;

                case "assistant" when msg.ToolCalls is { Count: > 0 }:
                    var openAiToolCalls = msg.ToolCalls.Select(tc =>
                        ChatToolCall.CreateFunctionToolCall(tc.Id, tc.Name, BinaryData.FromString(tc.ArgumentsJson))).ToList();
                    result.Add(ChatMessage.CreateAssistantMessage(openAiToolCalls));
                    break;

                case "assistant":
                    result.Add(ChatMessage.CreateAssistantMessage(msg.Content ?? string.Empty));
                    break;

                case "tool":
                    result.Add(ChatMessage.CreateToolMessage(msg.ToolCallId ?? string.Empty, msg.Content ?? string.Empty));
                    break;

                default:
                    result.Add(ChatMessage.CreateUserMessage(msg.Content ?? string.Empty));
                    break;
            }
        }

        return result;
    }

    private static ChatCompletionOptions BuildOptions(AiChatOptions options)
    {
        var completionOptions = new ChatCompletionOptions
        {
            Temperature = (float)options.Temperature,
            MaxOutputTokenCount = options.MaxTokens
        };

        if (options.Tools is { Count: > 0 })
        {
            foreach (var tool in options.Tools)
            {
                var schemaBytes = BinaryData.FromString(tool.ParameterSchema.GetRawText());
                completionOptions.Tools.Add(ChatTool.CreateFunctionTool(tool.Name, tool.Description, schemaBytes));
            }
        }

        return completionOptions;
    }

    private static string? ExtractTextContent(ChatMessageContent content)
    {
        var parts = content.Where(c => c.Kind == ChatMessageContentPartKind.Text).Select(c => c.Text);
        var text = string.Concat(parts);
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static IReadOnlyList<AiToolCall>? ExtractToolCalls(IReadOnlyList<ChatToolCall> toolCalls)
    {
        if (toolCalls is null or { Count: 0 }) return null;
        return toolCalls.Select(tc => new AiToolCall(tc.Id, tc.FunctionName, tc.FunctionArguments.ToString())).ToList();
    }

    private static string MapFinishReason(ChatFinishReason reason) => reason switch
    {
        ChatFinishReason.Stop => "stop",
        ChatFinishReason.ToolCalls => "tool_calls",
        ChatFinishReason.Length => "length",
        ChatFinishReason.ContentFilter => "content_filter",
        _ => reason.ToString().ToLowerInvariant()
    };
}
