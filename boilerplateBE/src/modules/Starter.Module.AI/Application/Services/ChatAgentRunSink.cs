using System.Text.Json;
using System.Threading.Channels;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Application.Services;

/// <summary>
/// Chat-layer implementation of IAgentRunSink. Persists intermediate assistant+tool
/// message rows as the runtime emits them, matching the legacy ChatExecutionService
/// behavior byte-for-byte. When streamWriter is non-null, also forwards stream frames
/// (delta, tool_call, tool_result) so ChatExecutionService.ExecuteStreamAsync can
/// surface them to the client as they arrive.
///
/// Finalization (final content row + citations + quota increment + title + webhooks)
/// remains the caller's responsibility — handled in ChatExecutionService.FinalizeTurnAsync.
/// </summary>
internal sealed class ChatAgentRunSink : IAgentRunSink
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AiDbContext _db;
    private readonly Guid _conversationId;
    private readonly ChannelWriter<ChatStreamEvent>? _streamWriter;
    private int _order;

    public ChatAgentRunSink(
        AiDbContext db,
        Guid conversationId,
        int startingOrder,
        ChannelWriter<ChatStreamEvent>? streamWriter)
    {
        _db = db;
        _conversationId = conversationId;
        _order = startingOrder;
        _streamWriter = streamWriter;
    }

    /// <summary>
    /// The next order value to use. ChatExecutionService.FinalizeTurnAsync reads this
    /// after RunAsync returns so it can persist the final content row at the correct position.
    /// </summary>
    public int NextOrder => _order;

    public Task OnStepStartedAsync(int stepIndex, CancellationToken ct) => Task.CompletedTask;

    public async Task OnAssistantMessageAsync(AgentAssistantMessage msg, CancellationToken ct)
    {
        // Only persist intermediate assistant-tool-call rows here. The final assistant
        // message (the one with no tool calls) is persisted by ChatExecutionService's
        // FinalizeTurnAsync so it can attach citations + invoke webhooks atomically.
        if (msg.ToolCalls.Count == 0) return;

        var json = JsonSerializer.Serialize(msg.ToolCalls, SerializerOptions);
        var row = AiMessage.CreateAssistantMessage(
            _conversationId,
            msg.Content ?? "",
            _order++,
            msg.InputTokens,
            msg.OutputTokens,
            toolCalls: json);

        _db.AiMessages.Add(row);
        await _db.SaveChangesAsync(ct);
    }

    public async Task OnToolCallAsync(AgentToolCallEvent call, CancellationToken ct)
    {
        if (_streamWriter is null) return;
        await _streamWriter.WriteAsync(new ChatStreamEvent("tool_call", new
        {
            CallId = call.Call.Id,
            Name = call.Call.Name,
            ArgumentsJson = call.Call.ArgumentsJson
        }), ct);
    }

    public async Task OnToolResultAsync(AgentToolResultEvent r, CancellationToken ct)
    {
        var row = AiMessage.CreateToolResultMessage(_conversationId, r.CallId, r.ResultJson, _order++);
        _db.AiMessages.Add(row);
        await _db.SaveChangesAsync(ct);

        if (_streamWriter is not null)
        {
            await _streamWriter.WriteAsync(new ChatStreamEvent("tool_result", new
            {
                CallId = r.CallId,
                IsError = r.IsError,
                Content = r.ResultJson
            }), ct);
        }
    }

    public async Task OnDeltaAsync(string d, CancellationToken ct)
    {
        if (_streamWriter is null) return;
        await _streamWriter.WriteAsync(new ChatStreamEvent("delta", new { Content = d }), ct);
    }

    public Task OnStepCompletedAsync(AgentStepEvent step, CancellationToken ct) => Task.CompletedTask;

    public Task OnRunCompletedAsync(AgentRunResult result, CancellationToken ct) => Task.CompletedTask;
}
