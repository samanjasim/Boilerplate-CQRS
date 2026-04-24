using System.Threading.Channels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Xunit;

namespace Starter.Api.Tests.Ai.Runtime;

public sealed class ChatAgentRunSinkTests
{
    private static AiDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"sink-{Guid.NewGuid()}")
            .Options;
        return new AiDbContext(options, currentUserService: null);
    }

    private static AiConversation SeedConversation(AiDbContext db)
    {
        var conv = AiConversation.Create(
            tenantId: Guid.NewGuid(),
            assistantId: Guid.NewGuid(),
            userId: Guid.NewGuid());
        db.AiConversations.Add(conv);
        db.SaveChanges();
        return conv;
    }

    [Fact]
    public async Task OnAssistantMessage_With_Tool_Calls_Persists_Assistant_Row()
    {
        using var db = BuildDb();
        var conv = SeedConversation(db);
        var sink = new ChatAgentRunSink(db, conv.Id, startingOrder: 0, streamWriter: null);

        await sink.OnAssistantMessageAsync(new AgentAssistantMessage(
            StepIndex: 0,
            Content: "thinking…",
            ToolCalls: new[] { new AiToolCall("c1", "search", """{"q":"x"}""") },
            InputTokens: 5,
            OutputTokens: 2),
            CancellationToken.None);

        await sink.OnStepCompletedAsync(
            new AgentStepEvent(
                StepIndex: 0,
                Kind: AgentStepKind.ToolCall,
                AssistantContent: null,
                ToolInvocations: Array.Empty<AgentToolInvocation>(),
                InputTokens: 0,
                OutputTokens: 0,
                FinishReason: "stop",
                StartedAt: DateTimeOffset.UtcNow,
                CompletedAt: DateTimeOffset.UtcNow),
            CancellationToken.None);

        var rows = await db.AiMessages.Where(m => m.ConversationId == conv.Id).ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Role.Should().Be(MessageRole.Assistant);
        rows[0].ToolCalls.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task OnAssistantMessage_Without_Tool_Calls_Does_Not_Persist()
    {
        using var db = BuildDb();
        var conv = SeedConversation(db);
        var sink = new ChatAgentRunSink(db, conv.Id, startingOrder: 0, streamWriter: null);

        await sink.OnAssistantMessageAsync(new AgentAssistantMessage(
            StepIndex: 0,
            Content: "final answer",
            ToolCalls: Array.Empty<AiToolCall>(),
            InputTokens: 5,
            OutputTokens: 2),
            CancellationToken.None);

        await sink.OnStepCompletedAsync(
            new AgentStepEvent(
                StepIndex: 0,
                Kind: AgentStepKind.Final,
                AssistantContent: null,
                ToolInvocations: Array.Empty<AgentToolInvocation>(),
                InputTokens: 0,
                OutputTokens: 0,
                FinishReason: "stop",
                StartedAt: DateTimeOffset.UtcNow,
                CompletedAt: DateTimeOffset.UtcNow),
            CancellationToken.None);

        var rows = await db.AiMessages.Where(m => m.ConversationId == conv.Id).ToListAsync();
        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task OnToolResult_Persists_Tool_Result_Row()
    {
        using var db = BuildDb();
        var conv = SeedConversation(db);
        var sink = new ChatAgentRunSink(db, conv.Id, startingOrder: 1, streamWriter: null);

        await sink.OnToolResultAsync(
            new AgentToolResultEvent(StepIndex: 0, CallId: "c1",
                ResultJson: """{"ok":true,"value":"hi"}""", IsError: false),
            CancellationToken.None);

        await sink.OnStepCompletedAsync(
            new AgentStepEvent(
                StepIndex: 0,
                Kind: AgentStepKind.ToolCall,
                AssistantContent: null,
                ToolInvocations: Array.Empty<AgentToolInvocation>(),
                InputTokens: 0,
                OutputTokens: 0,
                FinishReason: "stop",
                StartedAt: DateTimeOffset.UtcNow,
                CompletedAt: DateTimeOffset.UtcNow),
            CancellationToken.None);

        var rows = await db.AiMessages.Where(m => m.ConversationId == conv.Id).ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Role.Should().Be(MessageRole.ToolResult);
    }

    [Fact]
    public async Task Order_Monotonic_Across_Assistant_And_Tool_Rows()
    {
        using var db = BuildDb();
        var conv = SeedConversation(db);
        var sink = new ChatAgentRunSink(db, conv.Id, startingOrder: 10, streamWriter: null);

        // Step 0: assistant tool-call message + tool result
        await sink.OnAssistantMessageAsync(new AgentAssistantMessage(
            0, "t1", new[] { new AiToolCall("c1", "s", "{}") }, 1, 1), CancellationToken.None);
        await sink.OnToolResultAsync(new AgentToolResultEvent(0, "c1", """{"ok":true}""", false), CancellationToken.None);
        await sink.OnStepCompletedAsync(
            new AgentStepEvent(
                StepIndex: 0,
                Kind: AgentStepKind.ToolCall,
                AssistantContent: null,
                ToolInvocations: Array.Empty<AgentToolInvocation>(),
                InputTokens: 0,
                OutputTokens: 0,
                FinishReason: "stop",
                StartedAt: DateTimeOffset.UtcNow,
                CompletedAt: DateTimeOffset.UtcNow),
            CancellationToken.None);

        // Step 1: assistant tool-call message + tool result
        await sink.OnAssistantMessageAsync(new AgentAssistantMessage(
            1, "t2", new[] { new AiToolCall("c2", "s", "{}") }, 1, 1), CancellationToken.None);
        await sink.OnToolResultAsync(new AgentToolResultEvent(1, "c2", """{"ok":true}""", false), CancellationToken.None);
        await sink.OnStepCompletedAsync(
            new AgentStepEvent(
                StepIndex: 1,
                Kind: AgentStepKind.ToolCall,
                AssistantContent: null,
                ToolInvocations: Array.Empty<AgentToolInvocation>(),
                InputTokens: 0,
                OutputTokens: 0,
                FinishReason: "stop",
                StartedAt: DateTimeOffset.UtcNow,
                CompletedAt: DateTimeOffset.UtcNow),
            CancellationToken.None);

        var orders = await db.AiMessages.Where(m => m.ConversationId == conv.Id)
            .OrderBy(m => m.Order).Select(m => m.Order).ToListAsync();
        orders.Should().BeEquivalentTo(new[] { 10, 11, 12, 13 });
        sink.NextOrder.Should().Be(14);
    }

    [Fact]
    public async Task Streaming_Mode_Writes_ChatStreamEvent_Frames_On_Delta_ToolCall_ToolResult()
    {
        using var db = BuildDb();
        var conv = SeedConversation(db);
        var channel = Channel.CreateUnbounded<ChatStreamEvent>();
        var sink = new ChatAgentRunSink(db, conv.Id, startingOrder: 0, streamWriter: channel.Writer);

        await sink.OnDeltaAsync("hel", CancellationToken.None);
        await sink.OnToolCallAsync(new AgentToolCallEvent(0, new AiToolCall("c1", "s", "{}")), CancellationToken.None);
        await sink.OnToolResultAsync(new AgentToolResultEvent(0, "c1", """{"ok":true}""", false), CancellationToken.None);

        channel.Writer.TryComplete();
        var frames = new List<ChatStreamEvent>();
        await foreach (var f in channel.Reader.ReadAllAsync()) frames.Add(f);

        frames.Select(f => f.Type).Should().BeEquivalentTo(new[] { "delta", "tool_call", "tool_result" },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task NonStreaming_Mode_Does_Not_Write_Frames_On_Delta()
    {
        using var db = BuildDb();
        var conv = SeedConversation(db);
        var sink = new ChatAgentRunSink(db, conv.Id, startingOrder: 0, streamWriter: null);

        // Should be a no-op (no null deref, no exception)
        await sink.OnDeltaAsync("hello", CancellationToken.None);

        // No rows persisted because OnDeltaAsync isn't persistence-bearing
        (await db.AiMessages.CountAsync()).Should().Be(0);
    }
}
