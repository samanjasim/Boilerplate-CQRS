using FluentAssertions;
using Starter.Api.Tests.Ai.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Runtime;

/// <summary>
/// End-to-end test that drives <see cref="Starter.Module.AI.Application.Services.ChatExecutionService.ExecuteAsync"/>
/// with a scripted provider returning the same tool call repeatedly, and asserts the client sees
/// the standardized "step budget" message. This is the refactor's acceptance gate for
/// loop-break propagation through the entire chat turn.
/// </summary>
public sealed class AgentRuntimeEndToEndTests
{
    [Fact]
    public async Task Loop_Break_Surfaces_As_Step_Budget_Message_To_Client()
    {
        // Seed an assistant with enough steps to let the LoopBreakDetector window fill.
        // LoopBreakPolicy.Default requires 3 identical consecutive calls to trip.
        // MaxAgentSteps=10 ensures the runtime isn't capped before the detector can fire.
        var fx = new ChatExecutionTestFixture();
        var assistant = fx.SeedAssistantWithMaxSteps(maxAgentSteps: 10);

        // Script the provider to return the same tool call 5 times in a row.
        // The runtime's LoopBreakDetector (MaxIdenticalRepeats=3) will terminate
        // after the 3rd identical call and surface the step-budget message.
        for (var i = 0; i < 5; i++)
            fx.FakeProvider.EnqueueToolCall("search", """{"q":"loop"}""");

        var reply = await fx.RunOneTurnAsync(assistant, userMessage: "please search");

        reply.IsSuccess.Should().BeTrue();
        reply.Value!.AssistantMessage.Content.Should().Contain("couldn't fully complete");
    }
}
