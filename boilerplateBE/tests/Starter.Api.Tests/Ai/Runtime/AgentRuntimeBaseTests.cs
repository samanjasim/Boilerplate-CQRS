using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Runtime;
using Xunit;

namespace Starter.Api.Tests.Ai.Runtime;

public sealed class AgentRuntimeBaseTests
{
    private static AgentRunContext BuildCtx(
        IReadOnlyList<AiChatMessage>? messages = null,
        int maxSteps = 5,
        bool loopBreakEnabled = true)
    {
        return new AgentRunContext(
            Messages: messages ?? new[] { new AiChatMessage("user", "hi") },
            SystemPrompt: "you are helpful",
            ModelConfig: new AgentModelConfig(AiProviderType.OpenAI, "gpt-4o-mini", 0.7, 4096),
            Tools: new ToolResolutionResult(
                ProviderTools: Array.Empty<AiToolDefinitionDto>(),
                DefinitionsByName: new Dictionary<string, IAiToolDefinition>()),
            MaxSteps: maxSteps,
            LoopBreak: new LoopBreakPolicy(Enabled: loopBreakEnabled, MaxIdenticalRepeats: 3));
    }

    private static TestAgentRuntime BuildRuntime(
        FakeAiProvider provider,
        IAgentToolDispatcher? dispatcher = null)
    {
        dispatcher ??= Mock.Of<IAgentToolDispatcher>();
        var factory = new FakeAiProviderFactory(provider);
        return new TestAgentRuntime(factory, dispatcher, NullLogger<AgentRuntimeBase>.Instance);
    }

    [Fact]
    public async Task Single_Step_No_Tools_Returns_Completed()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("hello world");
        var sink = new RecordingSink();

        var result = await BuildRuntime(provider).RunAsync(BuildCtx(), sink, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
        result.FinalContent.Should().Be("hello world");
        result.Steps.Should().HaveCount(1);
        result.Steps[0].Kind.Should().Be(AgentStepKind.Final);
        sink.RunCompleted.Should().BeTrue();
        sink.StepStarted.Should().BeEquivalentTo(new[] { 0 });
        sink.AssistantMessages.Should().HaveCount(1);
        sink.AssistantMessages[0].ToolCalls.Should().BeEmpty();
        sink.ToolCalls.Should().BeEmpty();
        sink.ToolResults.Should().BeEmpty();
        sink.StepsCompleted.Should().HaveCount(1);
        sink.FinalResult.Should().NotBeNull();
    }

    [Fact]
    public async Task Tool_Call_Then_Final_Returns_Completed_With_Two_Steps()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueToolCall("search", """{"q":"x"}""");
        provider.EnqueueContent("done");

        var dispatcher = new Mock<IAgentToolDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<AiToolCall>(), It.IsAny<ToolResolutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolDispatchResult("""{"ok":true,"value":"ok"}""", false));

        var sink = new RecordingSink();
        var result = await BuildRuntime(provider, dispatcher.Object)
            .RunAsync(BuildCtx(), sink, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
        result.FinalContent.Should().Be("done");
        result.Steps.Should().HaveCount(2);
        result.Steps[0].Kind.Should().Be(AgentStepKind.ToolCall);
        result.Steps[0].ToolInvocations.Should().HaveCount(1);
        result.Steps[1].Kind.Should().Be(AgentStepKind.Final);
        sink.StepStarted.Should().BeEquivalentTo(new[] { 0, 1 });
        sink.AssistantMessages.Should().HaveCount(2);
        sink.AssistantMessages[0].ToolCalls.Should().HaveCount(1);
        sink.AssistantMessages[1].ToolCalls.Should().BeEmpty();
        sink.ToolCalls.Should().HaveCount(1);
        sink.ToolResults.Should().HaveCount(1);
        sink.ToolCalls[0].Call.Name.Should().Be("search");
        sink.ToolResults[0].CallId.Should().Be(sink.ToolCalls[0].Call.Id);
        sink.StepsCompleted.Should().HaveCount(2);
    }

    [Fact]
    public async Task Three_Identical_Tool_Calls_Trip_LoopBreak()
    {
        var provider = new FakeAiProvider();
        for (var i = 0; i < 5; i++) provider.EnqueueToolCall("search", """{"q":"x"}""");

        var dispatcher = new Mock<IAgentToolDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<AiToolCall>(), It.IsAny<ToolResolutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolDispatchResult("""{"ok":true,"value":null}""", false));

        var result = await BuildRuntime(provider, dispatcher.Object)
            .RunAsync(BuildCtx(), new RecordingSink(), CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.LoopBreak);
        result.TerminationReason.Should().Contain("search");
        result.Steps.Count.Should().BeInRange(3, 5);
    }

    [Fact]
    public async Task Max_Steps_Exceeded_Returns_Standard_Status()
    {
        var provider = new FakeAiProvider();
        for (var i = 0; i < 10; i++) provider.EnqueueToolCall("search", $$"""{"q":"{{i}}"}""");

        var dispatcher = new Mock<IAgentToolDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<AiToolCall>(), It.IsAny<ToolResolutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolDispatchResult("""{"ok":true,"value":null}""", false));

        var result = await BuildRuntime(provider, dispatcher.Object)
            .RunAsync(BuildCtx(maxSteps: 3), new RecordingSink(), CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.MaxStepsExceeded);
        result.Steps.Should().HaveCount(3);
    }

    [Fact]
    public async Task Provider_Throws_Returns_ProviderError_Status()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueThrow(new InvalidOperationException("provider broken"));

        var result = await BuildRuntime(provider)
            .RunAsync(BuildCtx(), new RecordingSink(), CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.ProviderError);
        result.TerminationReason.Should().Contain("provider broken");
    }

    [Fact]
    public async Task Tool_Dispatcher_Error_Continues_Loop()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueToolCall("search", """{"q":"x"}""");
        provider.EnqueueContent("done anyway");

        var dispatcher = new Mock<IAgentToolDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<AiToolCall>(), It.IsAny<ToolResolutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolDispatchResult("""{"ok":false,"error":{"code":"Ai.X","message":"nope"}}""", true));

        var result = await BuildRuntime(provider, dispatcher.Object)
            .RunAsync(BuildCtx(), new RecordingSink(), CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
        result.Steps[0].ToolInvocations[0].IsError.Should().BeTrue();
    }

    [Fact]
    public async Task Cancellation_Returns_Cancelled_Status()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("hi");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sink = new RecordingSink();
        var result = await BuildRuntime(provider).RunAsync(BuildCtx(), sink, cts.Token);

        result.Status.Should().Be(AgentRunStatus.Cancelled);
        // Pre-cancelled token must short-circuit before OnStepStartedAsync fires.
        sink.StepStarted.Should().BeEmpty();
        sink.AssistantMessages.Should().BeEmpty();
        sink.StepsCompleted.Should().BeEmpty();
        sink.RunCompleted.Should().BeTrue();   // OnRunCompletedAsync still fires once.
    }

    [Fact]
    public async Task Streaming_Single_Step_Emits_Delta_Events_And_Completes()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueStreamedContent("hello world", inputTokens: 7, outputTokens: 3);
        var sink = new RecordingSink();

        var ctx = BuildCtx() with { Streaming = true };
        var result = await BuildRuntime(provider).RunAsync(ctx, sink, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
        result.FinalContent.Should().Be("hello world");
        sink.Deltas.Should().ContainInOrder(new[] { "hello world" });
        result.TotalInputTokens.Should().Be(7);
        result.TotalOutputTokens.Should().Be(3);
    }

    [Fact]
    public async Task Streaming_Tool_Call_Step_Then_Content_Completes()
    {
        var provider = new FakeAiProvider();
        var id = "call-1";
        provider.EnqueueStreamChunks(new[]
        {
            new AiChatChunk(ContentDelta: null,
                ToolCallDelta: new AiToolCall(id, "search", """{"q":"x"}"""),
                FinishReason: null),
            new AiChatChunk(ContentDelta: null, ToolCallDelta: null,
                FinishReason: "tool_calls", InputTokens: 5, OutputTokens: 2)
        });
        provider.EnqueueStreamedContent("done");

        var dispatcher = new Mock<IAgentToolDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<AiToolCall>(), It.IsAny<ToolResolutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolDispatchResult("""{"ok":true,"value":"ok"}""", false));

        var sink = new RecordingSink();
        var ctx = BuildCtx() with { Streaming = true };
        var result = await BuildRuntime(provider, dispatcher.Object).RunAsync(ctx, sink, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
        result.FinalContent.Should().Be("done");
        result.Steps.Should().HaveCount(2);
        result.Steps[0].Kind.Should().Be(AgentStepKind.ToolCall);
        sink.ToolCalls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Streaming_Three_Identical_Tool_Calls_Trip_LoopBreak()
    {
        var provider = new FakeAiProvider();
        // Enqueue 5 streams, each with the same tool call
        for (var i = 0; i < 5; i++)
        {
            provider.EnqueueStreamChunks(new[]
            {
                new AiChatChunk(ContentDelta: null,
                    ToolCallDelta: new AiToolCall($"call-{i}", "search", """{"q":"x"}"""),
                    FinishReason: null),
                new AiChatChunk(ContentDelta: null, ToolCallDelta: null,
                    FinishReason: "tool_calls", InputTokens: 5, OutputTokens: 2)
            });
        }

        var dispatcher = new Mock<IAgentToolDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<AiToolCall>(), It.IsAny<ToolResolutionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolDispatchResult("""{"ok":true,"value":null}""", false));

        var ctx = BuildCtx() with { Streaming = true };
        var result = await BuildRuntime(provider, dispatcher.Object).RunAsync(ctx, new RecordingSink(), CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.LoopBreak);
        result.TerminationReason.Should().Contain("search");
    }
}

internal sealed class TestAgentRuntime : AgentRuntimeBase
{
    public TestAgentRuntime(
        IAiProviderFactory factory,
        IAgentToolDispatcher dispatcher,
        Microsoft.Extensions.Logging.ILogger<AgentRuntimeBase> logger)
        : base(factory, dispatcher, logger) { }
}

internal sealed class RecordingSink : IAgentRunSink
{
    public List<int> StepStarted { get; } = new();
    public List<AgentAssistantMessage> AssistantMessages { get; } = new();
    public List<AgentToolCallEvent> ToolCalls { get; } = new();
    public List<AgentToolResultEvent> ToolResults { get; } = new();
    public List<AgentStepEvent> StepsCompleted { get; } = new();
    public bool RunCompleted { get; private set; }
    public AgentRunResult? FinalResult { get; private set; }
    public List<string> Deltas { get; } = new();

    public Task OnStepStartedAsync(int i, CancellationToken ct) { StepStarted.Add(i); return Task.CompletedTask; }
    public Task OnAssistantMessageAsync(AgentAssistantMessage m, CancellationToken ct) { AssistantMessages.Add(m); return Task.CompletedTask; }
    public Task OnToolCallAsync(AgentToolCallEvent c, CancellationToken ct) { ToolCalls.Add(c); return Task.CompletedTask; }
    public Task OnToolResultAsync(AgentToolResultEvent r, CancellationToken ct) { ToolResults.Add(r); return Task.CompletedTask; }
    public Task OnDeltaAsync(string d, CancellationToken ct) { Deltas.Add(d); return Task.CompletedTask; }
    public Task OnStepCompletedAsync(AgentStepEvent s, CancellationToken ct) { StepsCompleted.Add(s); return Task.CompletedTask; }
    public Task OnRunCompletedAsync(AgentRunResult r, CancellationToken ct) { FinalResult = r; RunCompleted = true; return Task.CompletedTask; }
}
