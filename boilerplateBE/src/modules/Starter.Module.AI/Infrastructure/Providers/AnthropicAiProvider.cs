using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Starter.Module.AI.Infrastructure.Providers;

internal sealed class AnthropicAiProvider(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<AnthropicAiProvider> logger) : IAiProvider
{
    private const string DefaultModel = "claude-sonnet-4-20250514";

    private AnthropicClient CreateClient(string? resolvedApiKey = null)
    {
        var apiKey = !string.IsNullOrWhiteSpace(resolvedApiKey)
            ? resolvedApiKey
            : configuration["AI:Providers:Anthropic:ApiKey"]
              ?? throw new InvalidOperationException("Anthropic API key is not configured (AI:Providers:Anthropic:ApiKey).");
        // Use a pooled HttpClient so socket/DNS caches are shared and a single provider
        // instance does not spin up a new connection pool per call.
        var httpClient = httpClientFactory.CreateClient(nameof(AnthropicAiProvider));
        return new AnthropicClient(new APIAuthentication(apiKey), httpClient);
    }

    private string ResolveModel(string model)
    {
        // Callers may pass the provider-prefixed identifier from IAiProviderFactory.GetDefaultChatModelId()
        // (e.g. "Anthropic:claude-3-5-haiku-20241022"). That shape is a cache key, not a wire model name —
        // strip the prefix before sending to the Anthropic SDK.
        if (!string.IsNullOrWhiteSpace(model) && model.StartsWith("Anthropic:", StringComparison.OrdinalIgnoreCase))
            model = model["Anthropic:".Length..];

        if (!string.IsNullOrWhiteSpace(model) && model != DefaultModel)
            return model;
        return configuration["AI:Providers:Anthropic:DefaultModel"] ?? DefaultModel;
    }

    public async Task<AiChatCompletion> ChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        CancellationToken ct = default)
    {
        using var client = CreateClient(options.ApiKey);
        var parameters = BuildParameters(messages, options, stream: false);

        logger.LogDebug("Sending Anthropic chat request. Model={Model}, Messages={Count}", parameters.Model, parameters.Messages.Count);

        var response = await client.Messages.GetClaudeMessageAsync(parameters, ct);

        var content = ExtractTextContent(response.Content);
        var toolCalls = ExtractToolCalls(response.Content);
        var inputTokens = response.Usage?.InputTokens ?? 0;
        var outputTokens = response.Usage?.OutputTokens ?? 0;
        var finishReason = response.StopReason ?? "stop";

        return new AiChatCompletion(content, toolCalls, inputTokens, outputTokens, finishReason);
    }

    public async IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var client = CreateClient(options.ApiKey);
        var parameters = BuildParameters(messages, options, stream: true);

        logger.LogDebug("Starting Anthropic streaming request. Model={Model}", parameters.Model);

        string? currentToolId = null;
        string? currentToolName = null;
        int inputTokens = 0;
        int outputTokens = 0;

        await foreach (var response in client.Messages.StreamClaudeMessageAsync(parameters, ct))
        {
            // message_start: captures prompt-side usage (input_tokens, plus a rolling output_tokens starting at 0).
            if (response.StreamStartMessage?.Usage is { } startUsage)
            {
                inputTokens = startUsage.InputTokens;
                outputTokens = startUsage.OutputTokens;
            }

            // message_delta: carries the final cumulative output_tokens and the stop_reason.
            if (response.Usage is { } endUsage)
            {
                outputTokens = endUsage.OutputTokens;
            }

            // content_block_start carries the tool_use id/name; subsequent input_json_delta events belong to it.
            if (response.ContentBlock is { Type: "tool_use" } block)
            {
                currentToolId = block.Id;
                currentToolName = block.Name;
            }

            var delta = response.Delta;
            if (delta is null) continue;

            string? contentDelta = delta.Type == "text_delta" ? delta.Text : null;
            AiToolCall? toolCallDelta = null;

            if (delta.Type == "input_json_delta" && !string.IsNullOrEmpty(delta.PartialJson))
            {
                toolCallDelta = new AiToolCall(
                    currentToolId ?? string.Empty,
                    currentToolName ?? string.Empty,
                    delta.PartialJson);
            }

            string? finishReason = delta.StopReason;

            if (contentDelta is not null || toolCallDelta is not null || finishReason is not null)
            {
                // Attach final usage to the frame that carries finish_reason so the consumer gets one authoritative end-of-stream chunk.
                if (finishReason is not null)
                {
                    yield return new AiChatChunk(contentDelta, toolCallDelta, finishReason, inputTokens, outputTokens);
                }
                else
                {
                    yield return new AiChatChunk(contentDelta, toolCallDelta, finishReason);
                }
            }
        }
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => throw new NotSupportedException("Anthropic does not provide an embeddings API. Use OpenAI or Ollama for embeddings.");

    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => throw new NotSupportedException("Anthropic does not provide an embeddings API. Use OpenAI or Ollama for embeddings.");

    // ── Helpers ──────────────────────────────────────────────────────────────

    private MessageParameters BuildParameters(
        IReadOnlyList<AiChatMessage> messages,
        AiChatOptions options,
        bool stream)
    {
        var anthropicMessages = MapMessages(messages);
        var systemMessages = BuildSystemMessages(options.SystemPrompt);
        var tools = BuildTools(options.Tools);

        return new MessageParameters
        {
            Model = ResolveModel(options.Model),
            Messages = anthropicMessages,
            System = systemMessages,
            MaxTokens = options.MaxTokens,
            Temperature = (decimal)options.Temperature,
            Stream = stream,
            Tools = tools
        };
    }

    private static List<Message> MapMessages(IReadOnlyList<AiChatMessage> messages)
    {
        var result = new List<Message>(messages.Count);

        foreach (var msg in messages)
        {
            var role = msg.Role.ToLowerInvariant() switch
            {
                "assistant" => RoleType.Assistant,
                _ => RoleType.User
            };

            if (msg.ToolCalls is { Count: > 0 })
            {
                // assistant message with tool calls — build content list manually
                var content = new List<ContentBase>();
                if (!string.IsNullOrWhiteSpace(msg.Content))
                    content.Add(new TextContent { Text = msg.Content });

                foreach (var tc in msg.ToolCalls)
                {
                    content.Add(new ToolUseContent
                    {
                        Id = tc.Id,
                        Name = tc.Name,
                        Input = JsonNode.Parse(tc.ArgumentsJson)
                    });
                }

                result.Add(new Message { Role = RoleType.Assistant, Content = content });
            }
            else if (msg.ToolCallId is not null)
            {
                // tool result message
                var toolResultContent = new ToolResultContent
                {
                    ToolUseId = msg.ToolCallId,
                    Content = [new TextContent { Text = msg.Content ?? string.Empty }]
                };
                result.Add(new Message { Role = RoleType.User, Content = [toolResultContent] });
            }
            else
            {
                result.Add(new Message(role, msg.Content ?? string.Empty));
            }
        }

        return result;
    }

    private static List<SystemMessage>? BuildSystemMessages(string? systemPrompt)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
            return null;

        return [new SystemMessage(systemPrompt)];
    }

    private static IList<Anthropic.SDK.Common.Tool>? BuildTools(IReadOnlyList<AiToolDefinitionDto>? tools)
    {
        if (tools is null or { Count: 0 })
            return null;

        var result = new List<Anthropic.SDK.Common.Tool>(tools.Count);

        foreach (var tool in tools)
        {
            var schemaJson = tool.ParameterSchema.GetRawText();
            var schemaNode = JsonNode.Parse(schemaJson);
            var function = new Function(tool.Name, tool.Description, schemaNode);
            result.Add(new Anthropic.SDK.Common.Tool(function));
        }

        return result;
    }

    private static string? ExtractTextContent(List<ContentBase>? content)
    {
        if (content is null) return null;
        var parts = content.OfType<TextContent>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t));
        var text = string.Join(string.Empty, parts);
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static IReadOnlyList<AiToolCall>? ExtractToolCalls(List<ContentBase>? content)
    {
        if (content is null) return null;
        var toolUses = content.OfType<ToolUseContent>().ToList();
        if (toolUses.Count == 0) return null;

        return toolUses.Select(tu => new AiToolCall(
            tu.Id ?? string.Empty,
            tu.Name ?? string.Empty,
            tu.Input?.ToJsonString() ?? "{}")).ToList();
    }
}
