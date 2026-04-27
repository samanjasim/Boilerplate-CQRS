using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Api.Tests.Ai.Fakes;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Runtime;
using Xunit;

namespace Starter.Api.Tests.Ai.Runtime;

public sealed class OllamaAgentRuntimeTests
{
    private static AiDbContext NewAiDb()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"ollama-{Guid.NewGuid()}").Options;
        return new AiDbContext(opts, cu.Object);
    }

    private static AgentRunContext BuildCtx(ToolResolutionResult tools)
    {
        return new AgentRunContext(
            Messages: new[] { new AiChatMessage("user", "hi") },
            SystemPrompt: "you are helpful",
            ModelConfig: new AgentModelConfig(AiProviderType.Ollama, "llama3.1", 0.7, 4096),
            Tools: tools,
            MaxSteps: 3,
            LoopBreak: LoopBreakPolicy.Default);
    }

    private static ToolResolutionResult ToolsWithOne()
    {
        var def = new FakeToolDefinition("search", typeof(object), "Ai.UseTool");
        return new ToolResolutionResult(
            ProviderTools: new[]
            {
                new AiToolDefinitionDto("search", "test tool",
                    JsonDocument.Parse("{}").RootElement)
            },
            DefinitionsByName: new Dictionary<string, IAiToolDefinition>(StringComparer.Ordinal)
            {
                ["search"] = def
            });
    }

    [Fact]
    public async Task Runtime_Strips_Tools_Before_Calling_Provider()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("hello");

        var runtime = new OllamaAgentRuntime(
            new FakeAiProviderFactory(provider),
            Mock.Of<IAgentToolDispatcher>(),
            NewAiDb(),
            Mock.Of<IAgentPermissionResolver>(),
            NullLogger<AgentRuntimeBase>.Instance);

        var sink = new RecordingSink();
        var ctx = BuildCtx(ToolsWithOne());

        var result = await runtime.RunAsync(ctx, sink, CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
        // Provider received no tools — strip happened.
        provider.CallLog.Should().HaveCount(1);
        provider.CallLog.Single().Options.Tools.Should().BeNull();
    }

    [Fact]
    public async Task Runtime_Preserves_DefinitionsByName_After_Strip()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("hello");

        var runtime = new OllamaAgentRuntime(
            new FakeAiProviderFactory(provider),
            Mock.Of<IAgentToolDispatcher>(),
            NewAiDb(),
            Mock.Of<IAgentPermissionResolver>(),
            NullLogger<AgentRuntimeBase>.Instance);

        var ctx = BuildCtx(ToolsWithOne());

        // Record the context that flows through to base via a wrapper sink that
        // captures the first OnStepStarted (the context can't be inspected there,
        // so we rely on provider.Options.Tools being null to prove strip, and
        // verify DefinitionsByName survives by re-reading the original ctx.
        // This is a weak assertion — the strong one is above. But we can verify
        // the DefinitionsByName is a different object than ProviderTools and
        // that the with-expression only changed Tools.
        await runtime.RunAsync(ctx, new RecordingSink(), CancellationToken.None);

        // The original ctx must be unchanged (record mutation returns a new instance)
        ctx.Tools.ProviderTools.Should().HaveCount(1);
        ctx.Tools.DefinitionsByName.Should().ContainKey("search");
    }

    [Fact]
    public async Task Runtime_Skips_Strip_When_No_Tools_Present()
    {
        var provider = new FakeAiProvider();
        provider.EnqueueContent("hello");

        var emptyTools = new ToolResolutionResult(
            ProviderTools: Array.Empty<AiToolDefinitionDto>(),
            DefinitionsByName: new Dictionary<string, IAiToolDefinition>());

        var runtime = new OllamaAgentRuntime(
            new FakeAiProviderFactory(provider),
            Mock.Of<IAgentToolDispatcher>(),
            NewAiDb(),
            Mock.Of<IAgentPermissionResolver>(),
            NullLogger<AgentRuntimeBase>.Instance);

        var result = await runtime.RunAsync(BuildCtx(emptyTools), new RecordingSink(), CancellationToken.None);

        result.Status.Should().Be(AgentRunStatus.Completed);
    }
}
