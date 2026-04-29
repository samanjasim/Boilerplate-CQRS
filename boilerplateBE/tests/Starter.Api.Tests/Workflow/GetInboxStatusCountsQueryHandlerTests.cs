using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.Queries.GetInboxStatusCounts;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class GetInboxStatusCountsQueryHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly WorkflowDbContext _db = WorkflowEngineTestFactory.CreateDb();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly FakeTimeProvider _clock = new(Now);

    public GetInboxStatusCountsQueryHandlerTests()
    {
        _currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
    }

    [Fact]
    public async Task Handle_BucketsPendingTasksForCurrentUser()
    {
        var userId = _currentUser.Object.UserId!.Value;
        var otherUserId = Guid.NewGuid();

        _db.ApprovalTasks.AddRange(
            ApprovalTaskTestFactory.Pending(userId, dueDate: Now.AddHours(-2).UtcDateTime),
            ApprovalTaskTestFactory.Pending(userId, dueDate: Now.AddHours(3).UtcDateTime),
            ApprovalTaskTestFactory.Pending(userId, dueDate: Now.AddDays(2).UtcDateTime),
            ApprovalTaskTestFactory.Pending(userId),
            ApprovalTaskTestFactory.Pending(otherUserId, dueDate: Now.AddHours(-1).UtcDateTime));
        await _db.SaveChangesAsync();

        var sut = new GetInboxStatusCountsQueryHandler(_db, _currentUser.Object, _clock);
        var result = await sut.Handle(new GetInboxStatusCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Overdue);
        Assert.Equal(1, result.Value.DueToday);
        Assert.Equal(2, result.Value.Upcoming);
    }

    [Fact]
    public async Task Handle_IgnoresCompletedAndCancelledTasks()
    {
        var userId = _currentUser.Object.UserId!.Value;
        var completed = ApprovalTaskTestFactory.Pending(userId, dueDate: Now.AddHours(-2).UtcDateTime);
        completed.Complete("Approve", null, userId);
        var cancelled = ApprovalTaskTestFactory.Pending(userId, dueDate: Now.AddHours(2).UtcDateTime);
        cancelled.Cancel();

        _db.ApprovalTasks.AddRange(
            completed,
            cancelled,
            ApprovalTaskTestFactory.Pending(userId, dueDate: Now.AddDays(1).UtcDateTime));
        await _db.SaveChangesAsync();

        var sut = new GetInboxStatusCountsQueryHandler(_db, _currentUser.Object, _clock);
        var result = await sut.Handle(new GetInboxStatusCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Overdue);
        Assert.Equal(0, result.Value.DueToday);
        Assert.Equal(1, result.Value.Upcoming);
    }

    [Fact]
    public async Task Handle_ReturnsUnauthorizedWhenUserIsMissing()
    {
        _currentUser.SetupGet(x => x.UserId).Returns((Guid?)null);

        var sut = new GetInboxStatusCountsQueryHandler(_db, _currentUser.Object, _clock);
        var result = await sut.Handle(new GetInboxStatusCountsQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Error.Unauthorized", result.Error.Code);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
