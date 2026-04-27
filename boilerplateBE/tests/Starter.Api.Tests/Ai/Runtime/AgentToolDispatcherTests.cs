using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Runtime;
using Starter.Shared.Results;
using Xunit;

namespace Starter.Api.Tests.Ai.Runtime;

public sealed class AgentToolDispatcherTests
{
    private sealed record EchoCommand(string Text) : IRequest<Result<string>>;

    private static AgentToolDispatcher Build(
        Mock<ISender>? sender = null,
        Mock<IExecutionContext>? execution = null,
        bool hasPermission = true)
    {
        sender ??= new Mock<ISender>();
        execution ??= new Mock<IExecutionContext>();
        execution.Setup(e => e.HasPermission(It.IsAny<string>())).Returns(hasPermission);
        return new AgentToolDispatcher(
            sender.Object,
            execution.Object,
            NullLogger<AgentToolDispatcher>.Instance);
    }

    private static ToolResolutionResult BuildTools(string name, Type commandType, string permission)
    {
        var def = new FakeToolDefinition(name, commandType, permission);
        return new ToolResolutionResult(
            ProviderTools: [],
            DefinitionsByName: new Dictionary<string, IAiToolDefinition>(StringComparer.Ordinal)
            {
                [name] = def
            });
    }

    private static ToolResolutionResult EmptyTools() =>
        new(ProviderTools: [], DefinitionsByName: new Dictionary<string, IAiToolDefinition>());

    [Fact]
    public async Task Unknown_Tool_Returns_NotFound_Error()
    {
        var dispatcher = Build();
        var result = await dispatcher.DispatchAsync(
            new AiToolCall("id1", "missing", "{}"),
            EmptyTools(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Json.Should().Contain("Ai.ToolNotFound");
    }

    [Fact]
    public async Task Missing_Permission_Returns_PermissionDenied()
    {
        var dispatcher = Build(hasPermission: false);
        var tools = BuildTools("echo", typeof(EchoCommand), "Ai.UseTool");

        var result = await dispatcher.DispatchAsync(
            new AiToolCall("id1", "echo", """{"text":"hi"}"""),
            tools, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Json.Should().Contain("Ai.ToolPermissionDenied");
    }

    [Fact]
    public async Task Malformed_Args_Return_ArgumentsInvalid()
    {
        var dispatcher = Build();
        var tools = BuildTools("echo", typeof(EchoCommand), "Ai.UseTool");

        var result = await dispatcher.DispatchAsync(
            new AiToolCall("id1", "echo", "{not valid json"),
            tools, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Json.Should().Contain("Ai.ToolArgumentsInvalid");
    }

    [Fact]
    public async Task Successful_Result_Returns_Value_Json()
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Result.Success("echoed: hi"));
        var dispatcher = Build(sender);
        var tools = BuildTools("echo", typeof(EchoCommand), "Ai.UseTool");

        var result = await dispatcher.DispatchAsync(
            new AiToolCall("id1", "echo", """{"text":"hi"}"""),
            tools, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Json.Should().Contain("\"ok\":true");
        result.Json.Should().Contain("echoed: hi");
    }

    [Fact]
    public async Task Failed_Result_Returns_Error_Json()
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Result.Failure<string>(Error.Failure("Test.BadInput", "bad input")));
        var dispatcher = Build(sender);
        var tools = BuildTools("echo", typeof(EchoCommand), "Ai.UseTool");

        var result = await dispatcher.DispatchAsync(
            new AiToolCall("id1", "echo", """{"text":"hi"}"""),
            tools, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Json.Should().Contain("Test.BadInput");
    }

    [Fact]
    public async Task Handler_Throw_Returns_ExecutionFailed()
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("kaboom"));
        var dispatcher = Build(sender);
        var tools = BuildTools("echo", typeof(EchoCommand), "Ai.UseTool");

        var result = await dispatcher.DispatchAsync(
            new AiToolCall("id1", "echo", """{"text":"hi"}"""),
            tools, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Json.Should().Contain("Ai.ToolExecutionFailed");
    }
}

// FakeToolDefinition satisfies the real IAiToolDefinition interface from
// Starter.Abstractions.Capabilities. Includes all members:
//   Name, Description, ParameterSchema, CommandType, RequiredPermission, Category, IsReadOnly
internal sealed class FakeToolDefinition : IAiToolDefinition
{
    public FakeToolDefinition(string name, Type commandType, string permission)
    {
        Name = name;
        CommandType = commandType;
        RequiredPermission = permission;
    }

    public string Name { get; }
    public string Description => "test";
    public Type CommandType { get; }
    public string RequiredPermission { get; }
    public JsonElement ParameterSchema => JsonDocument.Parse("{}").RootElement;
    public string Category => "Test";
    public bool IsReadOnly => false;
}
