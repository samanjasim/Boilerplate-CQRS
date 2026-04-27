using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Attributes;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Commands.Approvals.ApprovePendingAction;
using Starter.Module.AI.Application.Commands.Approvals.DenyPendingAction;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Application.Services.Approvals;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Runtime;
using Starter.Shared.Results;
using Xunit;

namespace Starter.Api.Tests.Ai.AcidTests;

/// <summary>
/// Plan 5d-2 acid M4 — verifies the [DangerousAction] approval flow end-to-end:
///
///   1. Agent dispatches a <c>[DangerousAction]</c>-annotated MediatR command →
///      <see cref="AgentToolDispatcher"/> persists an <see cref="AiPendingApproval"/> row
///      instead of executing → run terminates AwaitingApproval.
///   2. <see cref="ApprovePendingActionCommand"/> re-dispatches the original command via
///      <see cref="ApprovalGrantExecutionContext"/> so the inner handler observes
///      <c>IExecutionContext.DangerousActionApprovalGrant == true</c>.
///   3. <see cref="DenyPendingActionCommand"/> rejects an empty reason at the validation
///      gate (<c>PendingApprovalErrors.DenyReasonRequired</c>).
///
/// The auto-deny-after-24h expiration path is covered by the Postgres-backed
/// <c>Plan5d2ApprovalExpirationAcidTests</c> (G2) and intentionally not duplicated here.
/// </summary>
public sealed class Plan5d2DangerousActionApprovalAcidTests
{
    // ────────────────────────────── shared wiring helpers ──────────────────────────────

    private static (AiDbContext db, Guid tenantId) MakeDb()
    {
        var tenantId = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.TenantId).Returns(tenantId);
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        return (new AiDbContext(opts, cu.Object), tenantId);
    }

    private static Mock<ICurrentAgentRunContextAccessor> MakeRunCtx(Guid tenantId)
    {
        var runCtx = new Mock<ICurrentAgentRunContextAccessor>();
        runCtx.SetupGet(x => x.AssistantId).Returns(Guid.NewGuid());
        runCtx.SetupGet(x => x.AssistantName).Returns("Tutor");
        runCtx.SetupGet(x => x.AgentPrincipalId).Returns(Guid.NewGuid());
        runCtx.SetupGet(x => x.ConversationId).Returns(Guid.NewGuid());
        runCtx.SetupGet(x => x.TenantId).Returns(tenantId);
        runCtx.SetupGet(x => x.RequestingUserId).Returns(Guid.NewGuid());
        return runCtx;
    }

    private static ToolResolutionResult BuildAcidTools()
    {
        var def = new AcidDangerousToolDefinition(
            name: "AcidDeleteAll",
            commandType: typeof(AcidFakeDeleteAllCommand),
            permission: "AnyPermission");
        return new ToolResolutionResult(
            ProviderTools: [],
            DefinitionsByName: new Dictionary<string, IAiToolDefinition>(StringComparer.Ordinal)
            {
                ["AcidDeleteAll"] = def
            });
    }

    // ────────────────────────────── tests ──────────────────────────────

    [Fact]
    public async Task Dispatch_Persists_Pending_And_Run_Awaits_Approval()
    {
        // Arrange — agent execution context with no approval grant active.
        var (db, tenantId) = MakeDb();
        var runCtx = MakeRunCtx(tenantId);

        var sender = new Mock<ISender>();
        var exec = new Mock<IExecutionContext>();
        exec.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
        exec.SetupGet(x => x.DangerousActionApprovalGrant).Returns(false);

        var approvals = new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var dispatcher = new AgentToolDispatcher(
            sender.Object, exec.Object, runCtx.Object, approvals, cfg,
            NullLogger<AgentToolDispatcher>.Instance);

        // Act
        var result = await dispatcher.DispatchAsync(
            new AiToolCall("call-acid-1", "AcidDeleteAll", """{"Confirm":true}"""),
            BuildAcidTools(), CancellationToken.None);

        // Assert — dispatch terminated AwaitingApproval, no command was sent.
        result.IsError.Should().BeTrue();
        result.AwaitingApproval.Should().BeTrue();
        result.ApprovalId.Should().NotBeNull();
        result.Json.Should().Contain("AiAgent.AwaitingApproval");
        sender.Verify(s => s.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);

        // Assert — pending row persisted with correct ToolName + Status=Pending.
        var pa = await db.AiPendingApprovals.SingleAsync();
        pa.Id.Should().Be(result.ApprovalId!.Value);
        pa.ToolName.Should().Be("AcidDeleteAll");
        pa.Status.Should().Be(PendingApprovalStatus.Pending);
        pa.CommandTypeName.Should().Contain(typeof(AcidFakeDeleteAllCommand).FullName!);
        pa.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task Approve_Reissues_Command_Via_Grant_Context()
    {
        // Arrange — seed a Pending approval row that captures the FakeDeleteAllCommand args.
        var (db, tenantId) = MakeDb();
        var assistantId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var requestingUser = Guid.NewGuid();

        var pa = AiPendingApproval.Create(
            tenantId: tenantId,
            assistantId: assistantId,
            assistantName: "Tutor",
            agentPrincipalId: principalId,
            conversationId: Guid.NewGuid(),
            agentTaskId: null,
            requestingUserId: requestingUser,
            toolName: "AcidDeleteAll",
            commandTypeName: typeof(AcidFakeDeleteAllCommand).AssemblyQualifiedName!,
            argumentsJson: """{"Confirm":true}""",
            reasonHint: "Test mass deletion",
            expiresAt: DateTime.UtcNow.AddHours(24));
        db.AiPendingApprovals.Add(pa);
        await db.SaveChangesAsync();

        // Wire a real MediatR ISender with our stub handler — the handler captures the
        // grant flag observed via AmbientExecutionContext at the moment of the inner Send.
        AcidFakeDeleteAllCommandHandler.Reset();
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AcidFakeDeleteAllCommand).Assembly));
        var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        // Decision-maker.
        var approverId = Guid.NewGuid();
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(approverId);
        cu.SetupGet(x => x.TenantId).Returns(tenantId);

        var approvals = new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance);

        // Install an ambient HTTP-style execution context (grant=false) so the handler's
        // ApprovalGrantExecutionContext wraps it for the one-shot re-dispatch.
        using var scope = AmbientExecutionContext.Use(new HttpAmbientStub(approverId, tenantId));

        var handler = new ApprovePendingActionCommandHandler(approvals, sender, cu.Object, db);

        // Act
        var result = await handler.Handle(new ApprovePendingActionCommand(pa.Id, Note: null), default);

        // Assert — approve succeeded; row flipped; inner handler observed grant=true exactly once.
        result.IsSuccess.Should().BeTrue();
        AcidFakeDeleteAllCommandHandler.InvocationCount.Should().Be(1,
            "the original FakeDeleteAllCommand should have been re-dispatched exactly once");
        AcidFakeDeleteAllCommandHandler.LastObservedGrant.Should().BeTrue(
            "the inner handler must observe IExecutionContext.DangerousActionApprovalGrant == true via the wrapped ambient");
        AcidFakeDeleteAllCommandHandler.LastObservedConfirm.Should().BeTrue(
            "the original command arguments must be reconstituted from ArgumentsJson");

        var refreshed = await db.AiPendingApprovals.AsNoTracking().SingleAsync(x => x.Id == pa.Id);
        refreshed.Status.Should().Be(PendingApprovalStatus.Approved);
        refreshed.DecisionUserId.Should().Be(approverId);
    }

    [Fact]
    public async Task Deny_Without_Reason_Returns_Validation_Failure()
    {
        // Arrange — handler with a real PendingApprovalService; row need not exist because
        // empty-reason validation runs before any DB work.
        var (db, _) = MakeDb();
        var svc = new PendingApprovalService(db, NullLogger<PendingApprovalService>.Instance);
        var cu = new Mock<ICurrentUserService>();
        cu.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        var handler = new DenyPendingActionCommandHandler(svc, cu.Object);

        // Act
        var result = await handler.Handle(new DenyPendingActionCommand(Guid.NewGuid(), ""), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PendingApproval.DenyReasonRequired");
    }

    // ────────────────────────────── stubs ──────────────────────────────

    /// <summary>
    /// Minimal ambient execution context used by the Approve test to stand in for the
    /// HTTP-side caller. Has <c>DangerousActionApprovalGrant=false</c>; the production
    /// <see cref="ApprovalGrantExecutionContext"/> wraps it during the re-dispatch.
    /// </summary>
    private sealed class HttpAmbientStub(Guid userId, Guid tenantId) : IExecutionContext
    {
        public Guid? UserId { get; } = userId;
        public Guid? AgentPrincipalId => null;
        public Guid? TenantId { get; } = tenantId;
        public Guid? AgentRunId => null;
        public bool DangerousActionApprovalGrant => false;
        public bool HasPermission(string permission) => true;
    }
}

/// <summary>
/// Local fake command, kept distinct from <c>FakeDeleteAllCommand</c> in
/// <c>Starter.Api.Tests.Ai.Approvals</c> (D3 dispatcher tests) so the MediatR
/// auto-registration in this acid test cannot collide with that handler-less type.
/// </summary>
[DangerousAction("Test mass deletion")]
public sealed record AcidFakeDeleteAllCommand(bool Confirm) : IRequest<Result>;

/// <summary>
/// Stub MediatR handler for <see cref="AcidFakeDeleteAllCommand"/>. Records whether
/// <see cref="AmbientExecutionContext.Current"/>.DangerousActionApprovalGrant was true
/// at invocation time so the Approve test can verify the grant flag was actually
/// installed by <see cref="ApprovePendingActionCommandHandler"/>.
/// </summary>
public sealed class AcidFakeDeleteAllCommandHandler : IRequestHandler<AcidFakeDeleteAllCommand, Result>
{
    public static int InvocationCount { get; private set; }
    public static bool? LastObservedGrant { get; private set; }
    public static bool? LastObservedConfirm { get; private set; }

    public static void Reset()
    {
        InvocationCount = 0;
        LastObservedGrant = null;
        LastObservedConfirm = null;
    }

    public Task<Result> Handle(AcidFakeDeleteAllCommand request, CancellationToken ct)
    {
        InvocationCount++;
        LastObservedGrant = AmbientExecutionContext.Current?.DangerousActionApprovalGrant;
        LastObservedConfirm = request.Confirm;
        return Task.FromResult(Result.Success());
    }
}

internal sealed class AcidDangerousToolDefinition : IAiToolDefinition
{
    public AcidDangerousToolDefinition(string name, Type commandType, string permission)
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
