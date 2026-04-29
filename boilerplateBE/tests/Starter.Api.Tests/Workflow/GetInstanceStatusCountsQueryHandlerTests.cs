using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.Queries.GetInstanceStatusCounts;
using Starter.Module.Workflow.Constants;
using Starter.Module.Workflow.Domain.Enums;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class GetInstanceStatusCountsQueryHandlerTests
{
    [Fact]
    public async Task Handle_BucketsActiveAwaitingCompletedAndCancelled()
    {
        var userId = Guid.NewGuid();
        await using var db = WorkflowEngineTestFactory.CreateDb();

        var activeNoTask = WorkflowInstanceTestFactory.Create(
            startedByUserId: userId,
            status: InstanceStatus.Active);
        var activeWithTask = WorkflowInstanceTestFactory.Create(
            startedByUserId: userId,
            status: InstanceStatus.Active);
        var completed = WorkflowInstanceTestFactory.Create(
            startedByUserId: userId,
            status: InstanceStatus.Completed);
        var cancelled = WorkflowInstanceTestFactory.Create(
            startedByUserId: userId,
            status: InstanceStatus.Cancelled);

        db.WorkflowInstances.AddRange(activeNoTask, activeWithTask, completed, cancelled);
        db.ApprovalTasks.Add(ApprovalTaskTestFactory.Pending(userId, instanceId: activeWithTask.Id));
        await db.SaveChangesAsync();

        var handler = new GetInstanceStatusCountsQueryHandler(db, CurrentUser(userId).Object);
        var result = await handler.Handle(new GetInstanceStatusCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Active);
        Assert.Equal(1, result.Value.Awaiting);
        Assert.Equal(1, result.Value.Completed);
        Assert.Equal(1, result.Value.Cancelled);
    }

    [Fact]
    public async Task Handle_StartedByUserIdScopesCountsToRequestedInitiator()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await using var db = WorkflowEngineTestFactory.CreateDb();

        db.WorkflowInstances.AddRange(
            WorkflowInstanceTestFactory.Create(userId, status: InstanceStatus.Active),
            WorkflowInstanceTestFactory.Create(userId, status: InstanceStatus.Completed),
            WorkflowInstanceTestFactory.Create(otherUserId, status: InstanceStatus.Active),
            WorkflowInstanceTestFactory.Create(otherUserId, status: InstanceStatus.Cancelled));
        await db.SaveChangesAsync();

        var handler = new GetInstanceStatusCountsQueryHandler(db, CurrentUser(userId, canViewAllTasks: true).Object);
        var result = await handler.Handle(
            new GetInstanceStatusCountsQuery(StartedByUserId: userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Active);
        Assert.Equal(0, result.Value.Awaiting);
        Assert.Equal(1, result.Value.Completed);
        Assert.Equal(0, result.Value.Cancelled);
    }

    [Fact]
    public async Task Handle_EntityTypeAndStateFiltersScopeCounts()
    {
        var userId = Guid.NewGuid();
        await using var db = WorkflowEngineTestFactory.CreateDb();

        db.WorkflowInstances.AddRange(
            WorkflowInstanceTestFactory.Create(userId, entityType: "Invoice", state: "Review", status: InstanceStatus.Active),
            WorkflowInstanceTestFactory.Create(userId, entityType: "Invoice", state: "Review", status: InstanceStatus.Completed),
            WorkflowInstanceTestFactory.Create(userId, entityType: "Invoice", state: "Draft", status: InstanceStatus.Cancelled),
            WorkflowInstanceTestFactory.Create(userId, entityType: "Product", state: "Review", status: InstanceStatus.Active));
        await db.SaveChangesAsync();

        var handler = new GetInstanceStatusCountsQueryHandler(db, CurrentUser(userId).Object);
        var result = await handler.Handle(
            new GetInstanceStatusCountsQuery(EntityType: "Invoice", State: "Review"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Active);
        Assert.Equal(0, result.Value.Awaiting);
        Assert.Equal(1, result.Value.Completed);
        Assert.Equal(0, result.Value.Cancelled);
    }

    [Fact]
    public async Task Handle_NonElevatedUserCannotOverrideStartedByUserId()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await using var db = WorkflowEngineTestFactory.CreateDb();

        db.WorkflowInstances.AddRange(
            WorkflowInstanceTestFactory.Create(userId, status: InstanceStatus.Active),
            WorkflowInstanceTestFactory.Create(otherUserId, status: InstanceStatus.Completed));
        await db.SaveChangesAsync();

        var handler = new GetInstanceStatusCountsQueryHandler(db, CurrentUser(userId).Object);
        var result = await handler.Handle(
            new GetInstanceStatusCountsQuery(StartedByUserId: otherUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Active);
        Assert.Equal(0, result.Value.Completed);
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorizedWhenUserIsMissing()
    {
        await using var db = WorkflowEngineTestFactory.CreateDb();

        var handler = new GetInstanceStatusCountsQueryHandler(db, CurrentUser(null).Object);
        var result = await handler.Handle(new GetInstanceStatusCountsQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Error.Unauthorized", result.Error.Code);
    }

    private static Mock<ICurrentUserService> CurrentUser(Guid? userId, bool canViewAllTasks = false)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser
            .Setup(x => x.HasPermission(WorkflowPermissions.ViewAllTasks))
            .Returns(canViewAllTasks);
        return currentUser;
    }
}
