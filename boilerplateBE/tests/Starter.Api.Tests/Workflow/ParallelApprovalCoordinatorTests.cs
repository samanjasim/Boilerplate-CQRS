using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;
using TaskStatus = Starter.Module.Workflow.Domain.Enums.TaskStatus;

namespace Starter.Api.Tests.Workflow;

public sealed class ParallelApprovalCoordinatorTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly ParallelApprovalCoordinator _sut;

    public ParallelApprovalCoordinatorTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);
        _sut = new ParallelApprovalCoordinator(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task EvaluateAsync_NoGroup_ReturnsProceed()
    {
        var task = CreateTask(groupId: null);
        var decision = await _sut.EvaluateAsync(task, parallelMode: "AllOf", action: "approve", CancellationToken.None);

        decision.ShouldProceed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_AnyOf_FirstCompletionCancelsSiblings()
    {
        var groupId = Guid.NewGuid();
        var myTask = CreateTask(groupId: groupId);
        var sibling1 = CreateTask(groupId: groupId);
        var sibling2 = CreateTask(groupId: groupId);

        var decision = await _sut.EvaluateAsync(myTask, parallelMode: "AnyOf", action: "approve", CancellationToken.None);

        decision.ShouldProceed.Should().BeTrue();
        sibling1.Status.Should().Be(TaskStatus.Cancelled);
        sibling2.Status.Should().Be(TaskStatus.Cancelled);
    }

    [Fact]
    public async Task EvaluateAsync_AllOf_WaitingForSiblings_ReturnsWait()
    {
        var groupId = Guid.NewGuid();
        var myTask = CreateTask(groupId: groupId);
        var sibling = CreateTask(groupId: groupId);

        var decision = await _sut.EvaluateAsync(myTask, parallelMode: "AllOf", action: "approve", CancellationToken.None);

        decision.ShouldProceed.Should().BeFalse();
        sibling.Status.Should().Be(TaskStatus.Pending);
    }

    [Fact]
    public async Task EvaluateAsync_AllOf_RejectShortCircuitsSiblings()
    {
        var groupId = Guid.NewGuid();
        var myTask = CreateTask(groupId: groupId);
        var sibling = CreateTask(groupId: groupId);

        var decision = await _sut.EvaluateAsync(myTask, parallelMode: "AllOf", action: "reject", CancellationToken.None);

        decision.ShouldProceed.Should().BeTrue();
        sibling.Status.Should().Be(TaskStatus.Cancelled);
    }

    [Fact]
    public async Task EvaluateAsync_AllOf_AllSiblingsCompleted_ReturnsProceed()
    {
        var groupId = Guid.NewGuid();
        var myTask = CreateTask(groupId: groupId);
        var sibling = CreateTask(groupId: groupId);
        sibling.Complete("approve", comment: null, userId: Guid.NewGuid());
        await _db.SaveChangesAsync();

        var decision = await _sut.EvaluateAsync(myTask, parallelMode: "AllOf", action: "approve", CancellationToken.None);

        decision.ShouldProceed.Should().BeTrue();
    }

    private ApprovalTask CreateTask(Guid? groupId)
    {
        var task = ApprovalTask.Create(
            tenantId: null,
            instanceId: Guid.NewGuid(),
            stepName: "Step",
            assigneeUserId: Guid.NewGuid(),
            assigneeRole: null,
            assigneeStrategyJson: null,
            entityType: "Order",
            entityId: Guid.NewGuid(),
            definitionName: "Def",
            availableActionsJson: "[]",
            groupId: groupId);
        _db.ApprovalTasks.Add(task);
        _db.SaveChanges();
        return task;
    }
}
