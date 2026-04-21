using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class HookExecutorTests
{
    private static HookContext MakeContext(
        Guid? assigneeUserId = null,
        Guid? actorUserId = null) => new(
        InstanceId: Guid.NewGuid(),
        EntityType: "Order",
        EntityId: Guid.NewGuid(),
        TenantId: Guid.NewGuid(),
        InitiatorUserId: Guid.NewGuid(),
        CurrentState: "Review",
        PreviousState: "Draft",
        Action: "submit",
        ActorUserId: actorUserId ?? Guid.NewGuid(),
        AssigneeUserId: assigneeUserId,
        AssigneeRole: null,
        DefinitionName: "OrderApproval");

    private static IConfiguration EmptyConfig()
        => new ConfigurationBuilder().Build();

    // ── 1. notify hook calls IMessageDispatcher ──────────────────────────────

    [Fact]
    public async Task Execute_NotifyHook_CallsMessageDispatcher()
    {
        var assigneeId = Guid.NewGuid();
        var ctx = MakeContext(assigneeUserId: assigneeId);

        var dispatcher = new Mock<IMessageDispatcher>();
        dispatcher
            .Setup(d => d.SendAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var sut = BuildSut(messageDispatcher: dispatcher.Object);
        var hooks = new List<HookConfig>
        {
            new("notify", Template: "state_entered", To: "assignee"),
        };

        await sut.ExecuteAsync(hooks, ctx, CancellationToken.None);

        dispatcher.Verify(
            d => d.SendAsync(
                "state_entered",
                assigneeId,
                It.IsAny<Dictionary<string, object>>(),
                ctx.TenantId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── 2. activity hook calls IActivityService ───────────────────────────────

    [Fact]
    public async Task Execute_ActivityHook_CallsActivityService()
    {
        var ctx = MakeContext();
        var activityService = new Mock<IActivityService>();

        var sut = BuildSut(activityService: activityService.Object);
        var hooks = new List<HookConfig>
        {
            new("activity", Action: "workflow_transition"),
        };

        await sut.ExecuteAsync(hooks, ctx, CancellationToken.None);

        activityService.Verify(
            a => a.RecordAsync(
                ctx.EntityType,
                ctx.EntityId,
                ctx.TenantId,
                "workflow_transition",
                ctx.ActorUserId,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── 3. webhook hook calls IWebhookPublisher ───────────────────────────────

    [Fact]
    public async Task Execute_WebhookHook_CallsWebhookPublisher()
    {
        var ctx = MakeContext();
        var publisher = new Mock<IWebhookPublisher>();

        var sut = BuildSut(webhookPublisher: publisher.Object);
        var hooks = new List<HookConfig>
        {
            new("webhook", Event: "order.submitted"),
        };

        await sut.ExecuteAsync(hooks, ctx, CancellationToken.None);

        publisher.Verify(
            p => p.PublishAsync(
                "order.submitted",
                ctx.TenantId,
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── 4. inAppNotify hook calls INotificationServiceCapability ─────────────

    [Fact]
    public async Task Execute_InAppNotifyHook_CallsNotificationService()
    {
        var assigneeId = Guid.NewGuid();
        var ctx = MakeContext(assigneeUserId: assigneeId);

        var notificationService = new Mock<INotificationServiceCapability>();

        var sut = BuildSut(notificationService: notificationService.Object);
        var hooks = new List<HookConfig>
        {
            new("inAppNotify", To: "assignee"),
        };

        await sut.ExecuteAsync(hooks, ctx, CancellationToken.None);

        notificationService.Verify(
            n => n.CreateAsync(
                assigneeId,
                ctx.TenantId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── 5. unknown hook type — no exception, no calls ─────────────────────────

    [Fact]
    public async Task Execute_UnknownHookType_LogsWarningAndSkips()
    {
        var ctx = MakeContext();
        var dispatcher = new Mock<IMessageDispatcher>();
        var activityService = new Mock<IActivityService>();

        var sut = BuildSut(
            messageDispatcher: dispatcher.Object,
            activityService: activityService.Object);

        var hooks = new List<HookConfig>
        {
            new("unknownType"),
        };

        var act = () => sut.ExecuteAsync(hooks, ctx, CancellationToken.None);
        await act.Should().NotThrowAsync();

        dispatcher.VerifyNoOtherCalls();
        activityService.VerifyNoOtherCalls();
    }

    // ── 6. hook throws — continues to next hook ───────────────────────────────

    [Fact]
    public async Task Execute_HookThrows_ContinuesToNextHook()
    {
        var ctx = MakeContext();

        var dispatcher = new Mock<IMessageDispatcher>();
        dispatcher
            .Setup(d => d.SendAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("dispatch failed"));

        var activityService = new Mock<IActivityService>();

        var sut = BuildSut(
            messageDispatcher: dispatcher.Object,
            activityService: activityService.Object);

        // first hook = notify (throws), second hook = activity (should still run)
        var assigneeId = Guid.NewGuid();
        var ctxWithAssignee = ctx with { AssigneeUserId = assigneeId };
        var hooks = new List<HookConfig>
        {
            new("notify", Template: "state_entered", To: "assignee"),
            new("activity", Action: "workflow_transition"),
        };

        await sut.ExecuteAsync(hooks, ctxWithAssignee, CancellationToken.None);

        activityService.Verify(
            a => a.RecordAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── 7. empty hook list — nothing called ──────────────────────────────────

    [Fact]
    public async Task Execute_EmptyHookList_DoesNothing()
    {
        var ctx = MakeContext();
        var dispatcher = new Mock<IMessageDispatcher>();
        var activityService = new Mock<IActivityService>();

        var sut = BuildSut(
            messageDispatcher: dispatcher.Object,
            activityService: activityService.Object);

        await sut.ExecuteAsync(new List<HookConfig>(), ctx, CancellationToken.None);

        dispatcher.VerifyNoOtherCalls();
        activityService.VerifyNoOtherCalls();
    }

    // ── 8. null hook list — nothing called ───────────────────────────────────

    [Fact]
    public async Task Execute_NullHookList_DoesNothing()
    {
        var ctx = MakeContext();
        var dispatcher = new Mock<IMessageDispatcher>();
        var activityService = new Mock<IActivityService>();

        var sut = BuildSut(
            messageDispatcher: dispatcher.Object,
            activityService: activityService.Object);

        await sut.ExecuteAsync(null, ctx, CancellationToken.None);

        dispatcher.VerifyNoOtherCalls();
        activityService.VerifyNoOtherCalls();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static HookExecutor BuildSut(
        IMessageDispatcher? messageDispatcher = null,
        IActivityService? activityService = null,
        IWebhookPublisher? webhookPublisher = null,
        INotificationServiceCapability? notificationService = null,
        IUserReader? userReader = null)
        => new(
            messageDispatcher ?? Mock.Of<IMessageDispatcher>(),
            activityService ?? Mock.Of<IActivityService>(),
            webhookPublisher ?? Mock.Of<IWebhookPublisher>(),
            notificationService ?? Mock.Of<INotificationServiceCapability>(),
            userReader ?? Mock.Of<IUserReader>(),
            EmptyConfig(),
            NullLogger<HookExecutor>.Instance);
}
