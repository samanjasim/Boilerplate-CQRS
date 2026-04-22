using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;
using Starter.Module.Workflow.Domain.Entities;
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
}
