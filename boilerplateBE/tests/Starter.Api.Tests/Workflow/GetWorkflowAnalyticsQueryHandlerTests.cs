using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Domain.Enums;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class GetWorkflowAnalyticsQueryHandlerTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly Mock<IUserReader> _userReader = new();
    private readonly GetWorkflowAnalyticsQueryHandler _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetWorkflowAnalyticsQueryHandlerTests()
    {
        _db = WorkflowEngineTestFactory.CreateDb();
        _userReader
            .Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Select(id => new UserSummary(
                    id, _tenantId, $"u{id:N}"[..8], $"u{id:N}@t"[..10],
                    DisplayName: $"User {id:N}"[..9], Status: "Active")).ToList());
        _sut = new GetWorkflowAnalyticsQueryHandler(_db, _userReader.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_UnknownDefinition_ReturnsDefinitionNotFound()
    {
        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(Guid.NewGuid(), WindowSelector.ThirtyDays),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Workflow.DefinitionNotFound");
    }

    [Fact]
    public async Task Handle_TemplateDefinition_ReturnsAnalyticsNotAvailableOnTemplate()
    {
        var template = WorkflowDefinition.Create(
            tenantId: null,
            name: "tpl",
            displayName: "Template",
            entityType: "General",
            statesJson: "[]",
            transitionsJson: "[]",
            isTemplate: true,
            sourceModule: "Tests");
        _db.WorkflowDefinitions.Add(template);
        await _db.SaveChangesAsync();

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(template.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Workflow.AnalyticsNotAvailableOnTemplate");
    }

    [Fact]
    public async Task Handle_EmptyDefinition_ReturnsZeroFilledDto()
    {
        var def = CreateTenantDefinition();

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.InstancesInWindow.Should().Be(0);
        result.Value.Headline.TotalStarted.Should().Be(0);
        result.Value.Headline.AvgCycleTimeHours.Should().BeNull();
        result.Value.StatesByBottleneck.Should().BeEmpty();
        result.Value.ActionRates.Should().BeEmpty();
        result.Value.StuckInstances.Should().BeEmpty();
        result.Value.ApproverActivity.Should().BeEmpty();
        // Series: 30-day window bucketed by day = 31 days (inclusive both ends).
        result.Value.InstanceCountSeries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_30DayWindow_HeadlineCountsMatchSeededStatusBreakdown()
    {
        var def = CreateTenantDefinition();
        var now = DateTime.UtcNow;

        SeedInstance(def.Id, now.AddDays(-1), InstanceStatus.Active);
        SeedInstance(def.Id, now.AddDays(-10), InstanceStatus.Completed,
            completedAt: now.AddDays(-10).AddHours(8));
        SeedInstance(def.Id, now.AddDays(-20), InstanceStatus.Completed,
            completedAt: now.AddDays(-20).AddHours(12));
        SeedInstance(def.Id, now.AddDays(-25), InstanceStatus.Cancelled,
            cancelledAt: now.AddDays(-25).AddHours(3));

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.InstancesInWindow.Should().Be(4);
        dto.Headline.TotalStarted.Should().Be(4);
        dto.Headline.TotalCompleted.Should().Be(2);
        dto.Headline.TotalCancelled.Should().Be(1);
        dto.Headline.AvgCycleTimeHours.Should().BeApproximately(10.0, 0.1); // (8+12)/2
    }

    [Fact]
    public async Task Handle_InstanceStartedBeforeWindow_IsExcludedEvenIfCompletedInside()
    {
        var def = CreateTenantDefinition();
        var now = DateTime.UtcNow;

        // Started 60 days ago (outside 30-day window) but completed 5 days ago.
        SeedInstance(def.Id, now.AddDays(-60), InstanceStatus.Completed,
            completedAt: now.AddDays(-5));
        // Started yesterday, still active.
        SeedInstance(def.Id, now.AddDays(-1), InstanceStatus.Active);

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        result.Value.InstancesInWindow.Should().Be(1);
        result.Value.Headline.TotalStarted.Should().Be(1);
        result.Value.Headline.TotalCompleted.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AllTimeWindow_UsesDefinitionCreatedAtAsStart()
    {
        var def = CreateTenantDefinition();
        var now = DateTime.UtcNow;

        // Backdate the definition so instances started 200 days ago fall within AllTime window.
        var defEntry = _db.Entry(def);
        defEntry.Property(nameof(WorkflowDefinition.CreatedAt)).CurrentValue = now.AddDays(-210);
        _db.SaveChanges();

        SeedInstance(def.Id, now.AddDays(-200), InstanceStatus.Completed,
            completedAt: now.AddDays(-199));

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.AllTime),
            CancellationToken.None);

        result.Value.WindowStart.Should().BeOnOrBefore(now.AddDays(-200));
        result.Value.InstancesInWindow.Should().Be(1);
        result.Value.Headline.TotalCompleted.Should().Be(1);
    }

    [Fact]
    public async Task Handle_30DayWindow_InstanceCountSeriesBucketsByDayAndIncludesStartedCompletedCancelled()
    {
        var def = CreateTenantDefinition();
        var today = DateTime.UtcNow.Date;

        // Two started today — use minutes-ago offsets so the StartedAt is always
        // strictly before DateTime.UtcNow regardless of the time-of-day the test runs.
        SeedInstance(def.Id, today.AddMinutes(1),  InstanceStatus.Active);
        SeedInstance(def.Id, today.AddMinutes(2), InstanceStatus.Active);
        // One started+completed yesterday.
        SeedInstance(def.Id, today.AddDays(-1).AddHours(5), InstanceStatus.Completed,
            completedAt: today.AddDays(-1).AddHours(9));
        // One started two days ago, cancelled today (minutes ago).
        SeedInstance(def.Id, today.AddDays(-2).AddHours(1), InstanceStatus.Cancelled,
            cancelledAt: today.AddMinutes(3));

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        var series = result.Value.InstanceCountSeries;
        var todayBucket     = series.Single(p => p.Bucket.Date == today);
        var yesterdayBucket = series.Single(p => p.Bucket.Date == today.AddDays(-1));
        var twoDaysBucket   = series.Single(p => p.Bucket.Date == today.AddDays(-2));

        todayBucket.Started.Should().Be(2);
        todayBucket.Cancelled.Should().Be(1);
        yesterdayBucket.Started.Should().Be(1);
        yesterdayBucket.Completed.Should().Be(1);
        twoDaysBucket.Started.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Bottlenecks_OnlyStatesWithThreeOrMoreVisitsAppear_OrderedByMedianDesc()
    {
        var def = CreateTenantDefinition();
        var now = DateTime.UtcNow;

        // Three instances dwell in "AwaitingApproval": 10h, 20h, 30h. Median=20, P95≈29.
        for (var i = 0; i < 3; i++)
        {
            var inst = SeedInstance(def.Id, now.AddDays(-5 - i), InstanceStatus.Active,
                initialState: "AwaitingApproval");
            // Enter "AwaitingApproval"
            SeedStep(inst.Id, "Draft", "AwaitingApproval",
                StepType.HumanTask, "Submit", actorUserId: null,
                timestamp: now.AddDays(-5 - i));
            // Exit "AwaitingApproval" after (10 + 10*i) hours
            SeedStep(inst.Id, "AwaitingApproval", "Approved",
                StepType.HumanTask, "approve", actorUserId: Guid.NewGuid(),
                timestamp: now.AddDays(-5 - i).AddHours(10 + 10 * i));
        }

        // Two instances dwell in "SeniorReview" (<3) — should be excluded.
        for (var i = 0; i < 2; i++)
        {
            var inst = SeedInstance(def.Id, now.AddDays(-4 - i), InstanceStatus.Active,
                initialState: "SeniorReview");
            SeedStep(inst.Id, "Draft", "SeniorReview",
                StepType.HumanTask, "Escalate", actorUserId: null, timestamp: now.AddDays(-4 - i));
            SeedStep(inst.Id, "SeniorReview", "Approved",
                StepType.HumanTask, "approve", actorUserId: Guid.NewGuid(),
                timestamp: now.AddDays(-4 - i).AddHours(5));
        }

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        result.Value.StatesByBottleneck.Should().HaveCount(1);
        var b = result.Value.StatesByBottleneck[0];
        b.StateName.Should().Be("AwaitingApproval");
        b.VisitCount.Should().Be(3);
        b.MedianDwellHours.Should().BeApproximately(20.0, 1.0);
        b.P95DwellHours.Should().BeGreaterThanOrEqualTo(20.0);
    }

    [Fact]
    public async Task Handle_ActionRates_PercentagesWithinStateSumToOne()
    {
        var def = CreateTenantDefinition();
        var now = DateTime.UtcNow;

        // Seed 10 completed human-task steps from "ManagerReview":
        // 7 approve, 3 reject.
        var instance = SeedInstance(def.Id, now.AddDays(-5), InstanceStatus.Active);
        for (var i = 0; i < 7; i++)
            SeedStep(instance.Id, "ManagerReview", "Approved",
                StepType.HumanTask, "approve", Guid.NewGuid(), now.AddDays(-5).AddMinutes(i));
        for (var i = 0; i < 3; i++)
            SeedStep(instance.Id, "ManagerReview", "Rejected",
                StepType.HumanTask, "reject", Guid.NewGuid(), now.AddDays(-5).AddMinutes(10 + i));

        // One SystemAction step must be ignored.
        SeedStep(instance.Id, "ManagerReview", "AutoEscalated",
            StepType.SystemAction, "autoEscalate", actorUserId: null, now.AddDays(-4));

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        var rates = result.Value.ActionRates.Where(r => r.StateName == "ManagerReview").ToList();
        rates.Should().HaveCount(2);
        rates.Single(r => r.Action == "approve").Count.Should().Be(7);
        rates.Single(r => r.Action == "reject").Count.Should().Be(3);
        rates.Sum(r => r.Percentage).Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public async Task Handle_StuckInstances_OrderedByStartedAtAsc_And_IncludeAssigneeDisplayName()
    {
        var def = CreateTenantDefinition();
        var now = DateTime.UtcNow;
        var alice = Guid.NewGuid();

        var oldest = SeedInstance(def.Id, now.AddDays(-10), InstanceStatus.Active);
        var middle = SeedInstance(def.Id, now.AddDays(-5),  InstanceStatus.Active);
        var newest = SeedInstance(def.Id, now.AddDays(-1),  InstanceStatus.Active);

        SeedPendingTask(oldest.Id, "AwaitingApproval", alice);
        SeedPendingTask(middle.Id, "AwaitingApproval", alice);
        SeedPendingTask(newest.Id, "AwaitingApproval", alice);

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        var stuck = result.Value.StuckInstances;
        stuck.Should().HaveCount(3);
        stuck[0].InstanceId.Should().Be(oldest.Id);
        stuck[1].InstanceId.Should().Be(middle.Id);
        stuck[2].InstanceId.Should().Be(newest.Id);
        stuck[0].DaysSinceStarted.Should().BeGreaterThanOrEqualTo(10);
        stuck[0].CurrentAssigneeDisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_StuckInstances_CappedAtTenRows()
    {
        var def = CreateTenantDefinition();
        var now = DateTime.UtcNow;

        for (var i = 0; i < 15; i++)
            SeedInstance(def.Id, now.AddDays(-i - 1), InstanceStatus.Active);

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        result.Value.StuckInstances.Should().HaveCount(10);
    }

    [Fact]
    public async Task Handle_ApproverActivity_CountsAndOrderByTotalActionsDesc()
    {
        var def = CreateTenantDefinition();
        var now = DateTime.UtcNow;
        var alice = Guid.NewGuid();
        var bob   = Guid.NewGuid();

        var inst = SeedInstance(def.Id, now.AddDays(-5), InstanceStatus.Active);

        // Alice: 3 approvals, 1 reject
        for (var i = 0; i < 3; i++)
            SeedStep(inst.Id, "ManagerReview", "Approved", StepType.HumanTask, "approve", alice,
                now.AddDays(-5).AddMinutes(i));
        SeedStep(inst.Id, "ManagerReview", "Rejected", StepType.HumanTask, "reject", alice,
            now.AddDays(-5).AddMinutes(5));

        // Bob: 1 return
        SeedStep(inst.Id, "ManagerReview", "Draft", StepType.HumanTask, "return", bob,
            now.AddDays(-4));

        // System action — must be excluded.
        SeedStep(inst.Id, "ManagerReview", "AutoArchived", StepType.SystemAction, "autoArchive",
            actorUserId: null, now.AddDays(-3));

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        var activity = result.Value.ApproverActivity;
        activity.Should().HaveCount(2);
        activity[0].UserId.Should().Be(alice);
        activity[0].Approvals.Should().Be(3);
        activity[0].Rejections.Should().Be(1);
        activity[0].Returns.Should().Be(0);
        activity[1].UserId.Should().Be(bob);
        activity[1].Returns.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ApproverActivity_AvgResponseTimeHours_WhenMatchedTask()
    {
        var def = CreateTenantDefinition();
        var now = DateTime.UtcNow;
        var alice = Guid.NewGuid();

        var inst = SeedInstance(def.Id, now.AddDays(-3), InstanceStatus.Active);

        // Seed a step for alice
        var stepTime = now.AddDays(-2);
        SeedStep(inst.Id, "Review", "Approved", StepType.HumanTask, "approve", alice, stepTime);

        // Seed a matching completed ApprovalTask:
        // - created 2 hours before the step timestamp
        // - completed exactly at the step timestamp
        var task = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: inst.Id,
            stepName: "Review",
            assigneeUserId: alice,
            assigneeRole: null,
            assigneeStrategyJson: null,
            entityType: "Order",
            entityId: inst.Id,
            definitionName: "analytics-test",
            availableActionsJson: "[]");
        _db.ApprovalTasks.Add(task);
        _db.SaveChanges();

        // Backdate CreatedAt 2 hours before stepTime
        _db.Entry(task).Property("CreatedAt").CurrentValue = stepTime.AddHours(-2);
        // Backdate CompletedAt to match stepTime exactly
        _db.Entry(task).Property("CompletedAt").CurrentValue = stepTime;
        _db.Entry(task).Property("CompletedByUserId").CurrentValue = alice;
        _db.Entry(task).Property("Status").CurrentValue = Starter.Module.Workflow.Domain.Enums.TaskStatus.Completed;
        _db.SaveChanges();

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var activity = result.Value.ApproverActivity;
        activity.Should().HaveCount(1);
        activity[0].UserId.Should().Be(alice);
        activity[0].AvgResponseTimeHours.Should().NotBeNull();
        activity[0].AvgResponseTimeHours.Should().BeApproximately(2.0, 0.1);
    }

    // ── Fixture helpers ──────────────────────────────────────────────────────

    private void SeedPendingTask(Guid instanceId, string stepName, Guid assigneeUserId)
    {
        var task = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instanceId,
            stepName: stepName,
            assigneeUserId: assigneeUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            entityType: "Order",
            entityId: Guid.NewGuid(),
            definitionName: "analytics-test",
            availableActionsJson: "[]");
        _db.ApprovalTasks.Add(task);
        _db.SaveChanges();
    }

    private void SeedStep(Guid instanceId, string fromState, string toState,
        StepType stepType, string action, Guid? actorUserId, DateTime timestamp)
    {
        var step = WorkflowStep.Create(instanceId, fromState, toState, stepType, action,
            actorUserId, comment: null, metadataJson: null);
        _db.WorkflowSteps.Add(step);
        _db.SaveChanges();
        _db.Entry(step).Property(nameof(WorkflowStep.Timestamp)).CurrentValue = timestamp;
        _db.SaveChanges();
    }

    private WorkflowDefinition CreateTenantDefinition()
    {
        var def = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "analytics-test",
            displayName: "Analytics Test",
            entityType: "Order",
            statesJson: "[]",
            transitionsJson: "[]",
            isTemplate: false,
            sourceModule: "Tests");
        _db.WorkflowDefinitions.Add(def);
        _db.SaveChanges();
        return def;
    }

    private WorkflowInstance SeedInstance(
        Guid definitionId,
        DateTime startedAt,
        InstanceStatus status = InstanceStatus.Active,
        DateTime? completedAt = null,
        DateTime? cancelledAt = null,
        string initialState = "Draft")
    {
        var instance = WorkflowInstance.Create(
            tenantId: _tenantId,
            definitionId: definitionId,
            entityType: "Order",
            entityId: Guid.NewGuid(),
            initialState: initialState,
            startedByUserId: Guid.NewGuid(),
            contextJson: null,
            definitionName: "analytics-test");
        _db.WorkflowInstances.Add(instance);
        _db.SaveChanges();

        // Backdate StartedAt via SQL-style direct field write; EF InMemory allows
        // rewriting tracked property values here because the shadow fields are
        // open. Avoids a new mutator on the aggregate.
        var entry = _db.Entry(instance);
        entry.Property(nameof(WorkflowInstance.StartedAt)).CurrentValue = startedAt;
        if (status == InstanceStatus.Completed)
        {
            entry.Property(nameof(WorkflowInstance.Status)).CurrentValue = InstanceStatus.Completed;
            entry.Property(nameof(WorkflowInstance.CompletedAt)).CurrentValue =
                completedAt ?? startedAt.AddHours(10);
        }
        else if (status == InstanceStatus.Cancelled)
        {
            entry.Property(nameof(WorkflowInstance.Status)).CurrentValue = InstanceStatus.Cancelled;
            entry.Property(nameof(WorkflowInstance.CancelledAt)).CurrentValue =
                cancelledAt ?? startedAt.AddHours(5);
        }
        _db.SaveChanges();
        return instance;
    }
}
