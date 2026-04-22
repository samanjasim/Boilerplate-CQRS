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

        SeedInstance(def.Id, now.AddDays(-200), InstanceStatus.Completed,
            completedAt: now.AddDays(-199));

        var result = await _sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.AllTime),
            CancellationToken.None);

        result.Value.WindowStart.Should().BeOnOrBefore(def.CreatedAt.AddSeconds(1));
        result.Value.InstancesInWindow.Should().Be(1);
        result.Value.Headline.TotalCompleted.Should().Be(1);
    }

    // ── Fixture helpers ──────────────────────────────────────────────────────

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
