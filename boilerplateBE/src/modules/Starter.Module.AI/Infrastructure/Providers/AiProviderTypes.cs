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

internal sealed record AiChatChunk(
    string? ContentDelta,
    AiToolCall? ToolCallDelta,
    string? FinishReason);

internal sealed record AiToolCall(
    string Id,
    string Name,
    string ArgumentsJson);

internal sealed record AiToolDefinitionDto(
    string Name,
    string Description,
    JsonElement ParameterSchema);
