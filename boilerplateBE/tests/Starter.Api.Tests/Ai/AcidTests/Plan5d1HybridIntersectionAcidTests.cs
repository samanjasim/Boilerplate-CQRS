using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Runtime;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-1 acid test M1: hybrid-intersection security through the agent dispatcher.
///
/// Scenarios exercised against the real AgentToolDispatcher with an AgentExecutionScope
/// installed via the core AmbientExecutionContext holder. These map directly to the
/// flagship acid tests in spec §9.1.
/// </summary>
public sealed class Plan5d1HybridIntersectionAcidTests
{
    private sealed record FakeToolCommand(string Echo) : IRequest<Starter.Shared.Results.Result<string>>;

    private static IAgentToolDispatcher BuildDispatcher(IExecutionContext execution)
    {
        var sender = new Mock<ISender>();
        return new AgentToolDispatcher(
            sender.Object, execution, NullLogger<AgentToolDispatcher>.Instance);
    }

    private static ToolResolutionResult ToolsRequiringPermission(string toolName, string permission)
    {
        var def = new Mock<IAiToolDefinition>();
        def.SetupGet(x => x.Name).Returns(toolName);
        def.SetupGet(x => x.RequiredPermission).Returns(permission);
        def.SetupGet(x => x.CommandType).Returns(typeof(FakeToolCommand));
        return new ToolResolutionResult(
            ProviderTools: Array.Empty<AiToolDefinitionDto>(),
            DefinitionsByName: new Dictionary<string, IAiToolDefinition>(StringComparer.Ordinal)
            {
                [toolName] = def.Object
            });
    }

    [Fact]
    public async Task Acid_M1_1_Caller_Restricts_Agent_When_Caller_Lacks_Permission()
    {
        // Caller can only Read; agent has Read AND Delete. Intersection blocks Delete.
        using var scope = AgentExecutionScope.Begin(
            userId: Guid.NewGuid(),
            agentPrincipalId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            callerHasPermission: p => p == "Files.Read",
            agentHasPermission: p => p is "Files.Read" or "Files.Delete");

        var dispatcher = BuildDispatcher(AmbientExecutionContext.Current!);
        var tools = ToolsRequiringPermission("delete_file", "Files.Delete");

        var result = await dispatcher.DispatchAsync(
            new AiToolCall("call-1", "delete_file", "{\"echo\":\"x\"}"),
            tools, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Json.Should().Contain("Ai.ToolPermissionDenied");
    }

    [Fact]
    public async Task Acid_M1_2_Operational_Run_Uses_Agent_Permissions_Only()
    {
        // No caller (operational): agent can Read; dispatcher allows. Agent lacks Delete: refused.
        using var scope = AgentExecutionScope.Begin(
            userId: null,
            agentPrincipalId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            callerHasPermission: null,
            agentHasPermission: p => p == "Files.Read");

        var dispatcher = BuildDispatcher(AmbientExecutionContext.Current!);

        var readResult = await dispatcher.DispatchAsync(
            new AiToolCall("call-2", "read_file", "{\"echo\":\"x\"}"),
            ToolsRequiringPermission("read_file", "Files.Read"),
            CancellationToken.None);
        readResult.IsError.Should().BeFalse(); // agent has it, no caller required

        var deleteResult = await dispatcher.DispatchAsync(
            new AiToolCall("call-3", "delete_file", "{\"echo\":\"x\"}"),
            ToolsRequiringPermission("delete_file", "Files.Delete"),
            CancellationToken.None);
        deleteResult.IsError.Should().BeTrue();
        deleteResult.Json.Should().Contain("Ai.ToolPermissionDenied");
    }

    [Fact]
    public async Task Acid_M1_3_Outside_Agent_Scope_Falls_Back_To_Default_Execution_Context()
    {
        // No active AgentExecutionScope — IExecutionContext is HttpExecutionContext / mock.
        var execution = new Mock<IExecutionContext>();
        execution.Setup(x => x.HasPermission("Files.Read")).Returns(true);
        var dispatcher = BuildDispatcher(execution.Object);

        var result = await dispatcher.DispatchAsync(
            new AiToolCall("call-4", "read_file", "{\"echo\":\"x\"}"),
            ToolsRequiringPermission("read_file", "Files.Read"),
            CancellationToken.None);

        // Tool execution depends on ISender returning a value — we just assert the
        // permission check passes (no PermissionDenied error).
        result.Json.Should().NotContain("Ai.ToolPermissionDenied");
    }
}
