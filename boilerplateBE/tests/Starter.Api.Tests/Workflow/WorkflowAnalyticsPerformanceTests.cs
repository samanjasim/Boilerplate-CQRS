using System.Diagnostics;
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

[Trait("perf", "true")]
public sealed class WorkflowAnalyticsPerformanceTests : IDisposable
{
    private readonly WorkflowDbContext _db = WorkflowEngineTestFactory.CreateDb();
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_10kInstances_CompletesUnderOneSecond()
    {
        var tenant = Guid.NewGuid();
        var def = WorkflowDefinition.Create(
            tenantId: tenant, name: "perf", displayName: "Perf", entityType: "Order",
            statesJson: "[]", transitionsJson: "[]", isTemplate: false, sourceModule: "Perf");
        _db.WorkflowDefinitions.Add(def);
        await _db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        for (var i = 0; i < 10_000; i++)
        {
            var inst = WorkflowInstance.Create(
                tenantId: tenant, definitionId: def.Id, entityType: "Order",
                entityId: Guid.NewGuid(), initialState: "Draft",
                startedByUserId: Guid.NewGuid(), contextJson: null,
                definitionName: "perf");
            _db.WorkflowInstances.Add(inst);
        }
        await _db.SaveChangesAsync();

        // Seed ~20k steps (2 per instance) covering HumanTask actions for bottleneck + approver aggregations.
        foreach (var inst in _db.WorkflowInstances.AsNoTracking().ToList())
        {
            _db.WorkflowSteps.Add(WorkflowStep.Create(inst.Id, "Draft", "Review",
                StepType.HumanTask, "Submit", Guid.NewGuid(), comment: null, metadataJson: null));
            _db.WorkflowSteps.Add(WorkflowStep.Create(inst.Id, "Review", "Approved",
                StepType.HumanTask, "approve", Guid.NewGuid(), comment: null, metadataJson: null));
        }
        await _db.SaveChangesAsync();

        var userReader = new Mock<IUserReader>();
        userReader.Setup(r => r.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserSummary>());
        var sut = new GetWorkflowAnalyticsQueryHandler(_db, userReader.Object);

        var sw = Stopwatch.StartNew();
        var result = await sut.Handle(
            new GetWorkflowAnalyticsQuery(def.Id, WindowSelector.ThirtyDays),
            CancellationToken.None);
        sw.Stop();

        result.IsSuccess.Should().BeTrue();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1),
            "10k-instance analytics must complete under the 1s budget; if it doesn't, see the spec's deferred 'snapshot table' item.");
    }
}
