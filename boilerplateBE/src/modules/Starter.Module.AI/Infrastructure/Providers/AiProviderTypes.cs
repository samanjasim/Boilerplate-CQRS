using System.Text.Json;

namespace Starter.Module.AI.Infrastructure.Providers;

internal sealed record AiChatMessage(string Role, string? Content, string? ToolCallId = null, IReadOnlyList<AiToolCall>? ToolCalls = null);

internal sealed record AiChatOptions(
    string Model,
    double Temperature = 0.7,
    int MaxTokens = 4096,
    string? SystemPrompt = null,
    IReadOnlyList<AiToolDefinitionDto>? Tools = null);

internal sealed record AiChatCompletion(
    string? Content,
    IReadOnlyList<AiToolCall>? ToolCalls,
    int InputTokens,
    int OutputTokens,
    string FinishReason);

/// <summary>
/// A single frame from a streaming chat response. Any field may be null — providers
/// emit multiple chunks that collectively cover content, tool calls, final usage,
/// and a finish reason. Usage fields are populated by an "end-of-stream" chunk
/// that most providers send after the last content delta.
/// </summary>
internal sealed record AiChatChunk(
    string? ContentDelta,
    AiToolCall? ToolCallDelta,
    string? FinishReason,
    int? InputTokens = null,
    int? OutputTokens = null);

internal sealed record AiToolCall(
    string Id,
    string Name,
    string ArgumentsJson);

internal sealed record AiToolDefinitionDto(
    string Name,
    string Description,
    JsonElement ParameterSchema);
