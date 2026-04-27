using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Attributes;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Runtime;
using Starter.Shared.Results;
using Xunit;

namespace Starter.Api.Tests.Ai.Approvals;

[DangerousAction("Test mass deletion")]
public sealed record FakeDeleteAllCommand(bool Confirm) : IRequest<Result>;

public sealed class AgentToolDispatcherDangerousActionTests
{
    private static (AiDbContext db, Mock<ICurrentAgentRunContextAccessor> runCtx) MakeDb()
    {
        var tenantId = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        // Match AiDbContext's CurrentTenantId so the AiPendingApproval query filter resolves.
        cu.SetupGet(x => x.TenantId).Returns(tenantId);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);

        var runCtx = new Mock<ICurrentAgentRunContextAccessor>();
        runCtx.SetupGet(x => x.AssistantId).Returns(Guid.NewGuid());
        runCtx.SetupGet(x => x.AssistantName).Returns("Tutor");
        runCtx.SetupGet(x => x.AgentPrincipalId).Returns(Guid.NewGuid());
        runCtx.SetupGet(x => x.ConversationId).Returns(Guid.NewGuid());
        runCtx.SetupGet(x => x.TenantId).Returns(tenantId);
        runCtx.SetupGet(x => x.RequestingUserId).Returns(Guid.NewGuid());
        return (db, runCtx);
    }

    private static ToolResolutionResult BuildTools()
    {
        var def = new FakeDangerousToolDefinition(
            name: "DeleteAll",
            commandType: typeof(FakeDeleteAllCommand),
            permission: "AnyPermission");
        return new ToolResolutionResult(
            ProviderTools: [],
            DefinitionsByName: new Dictionary<string, IAiToolDefinition>(StringComparer.Ordinal)
            {
                ["DeleteAll"] = def
            });
    }

    [Fact]
    public async Task Persists_Approval_And_Returns_Awaiting()
    {
        var (db, runCtx) = MakeDb();
        var sender = new Mock<ISender>();
        var exec = new Mock<IExecutionContext>();
        exec.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
        exec.SetupGet(x => x.DangerousActionApprovalGrant).Returns(false);

        var approvals = new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var dispatcher = new AgentToolDispatcher(
            sender.Object, exec.Object, runCtx.Object, approvals, cfg,
            NullLogger<AgentToolDispatcher>.Instance);

        var result = await dispatcher.DispatchAsync(
            new AiToolCall("call-1", "DeleteAll", """{"Confirm":true}"""),
            BuildTools(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.AwaitingApproval.Should().BeTrue();
        result.ApprovalId.Should().NotBeNull();
        result.Json.Should().Contain("AiAgent.AwaitingApproval");
        sender.Verify(s => s.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);

        var pa = await db.AiPendingApprovals.FirstAsync();
        pa.ToolName.Should().Be("DeleteAll");
        pa.Id.Should().Be(result.ApprovalId!.Value);
    }

    [Fact]
    public async Task Skip_Check_When_Grant_Is_Active()
    {
        var (db, runCtx) = MakeDb();
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Result.Success());

        var exec = new Mock<IExecutionContext>();
        exec.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
        exec.SetupGet(x => x.DangerousActionApprovalGrant).Returns(true);

        var approvals = new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var dispatcher = new AgentToolDispatcher(
            sender.Object, exec.Object, runCtx.Object, approvals, cfg,
            NullLogger<AgentToolDispatcher>.Instance);

        var result = await dispatcher.DispatchAsync(
            new AiToolCall("call-1", "DeleteAll", """{"Confirm":true}"""),
            BuildTools(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.AwaitingApproval.Should().BeFalse();
        result.ApprovalId.Should().BeNull();
        sender.Verify(s => s.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        (await db.AiPendingApprovals.AnyAsync()).Should().BeFalse();
    }
}

internal sealed class FakeDangerousToolDefinition : IAiToolDefinition
{
    public FakeDangerousToolDefinition(string name, Type commandType, string permission)
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
