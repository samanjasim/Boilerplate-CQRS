using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.Commands.BatchExecuteTasks;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class BatchExecuteTasksTests : IDisposable
{
    private readonly Mock<IWorkflowService> _workflow = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly WorkflowDbContext _db;
    private readonly Guid _userId = Guid.NewGuid();

    public BatchExecuteTasksTests()
    {
        _currentUser.SetupGet(x => x.UserId).Returns(_userId);
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private BatchExecuteTasksCommandHandler Handler() => new(
        _workflow.Object,
        _db,
        _currentUser.Object,
        NullLogger<BatchExecuteTasksCommandHandler>.Instance);

    [Fact]
    public async Task Handle_AllSucceed_ReturnsAllSucceeded()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _workflow
            .Setup(w => w.ExecuteTaskAsync(It.IsAny<Guid>(), "approve", null, _userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkflowTaskResult.Success());

        var result = await Handler().Handle(
            new BatchExecuteTasksCommand(ids, "approve"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var r = result.Value!;
        r.Succeeded.Should().Be(3);
        r.Failed.Should().Be(0);
        r.Skipped.Should().Be(0);
        r.Items.Should().HaveCount(3).And.OnlyContain(i => i.Status == "Succeeded");
    }

    [Fact]
    public async Task Handle_MixedResults_AggregatesCorrectly()
    {
        var success = Guid.NewGuid();
        var fail = Guid.NewGuid();
        var throws = Guid.NewGuid();

        _workflow.Setup(w => w.ExecuteTaskAsync(success, "approve", null, _userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(WorkflowTaskResult.Success());
        _workflow.Setup(w => w.ExecuteTaskAsync(fail, "approve", null, _userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(WorkflowTaskResult.Failure("Workflow.TaskNotFound", $"Approval task '{fail}' not found", WorkflowErrorKind.NotFound));
        _workflow.Setup(w => w.ExecuteTaskAsync(throws, "approve", null, _userId, null, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("nope"));

        var result = await Handler().Handle(
            new BatchExecuteTasksCommand(new[] { success, fail, throws }, "approve"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var r = result.Value!;
        r.Succeeded.Should().Be(1);
        r.Failed.Should().Be(2);
        r.Skipped.Should().Be(0);
        r.Items.Single(i => i.TaskId == success).Status.Should().Be("Succeeded");
        r.Items.Single(i => i.TaskId == fail).Status.Should().Be("Failed");
        // Exception messages must NEVER leak to clients — assert generic message.
        r.Items.Single(i => i.TaskId == throws).Error.Should().NotContain("nope");
        r.Items.Single(i => i.TaskId == throws).Error.Should().Contain("unexpected");
    }

    [Fact]
    public async Task Handle_OneExceptionDoesNotAbortBatch()
    {
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();
        _workflow.Setup(w => w.ExecuteTaskAsync(bad, It.IsAny<string>(), null, _userId, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));
        _workflow.Setup(w => w.ExecuteTaskAsync(good, It.IsAny<string>(), null, _userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkflowTaskResult.Success());

        var result = await Handler().Handle(
            new BatchExecuteTasksCommand(new[] { bad, good }, "approve"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Succeeded.Should().Be(1);
        result.Value.Failed.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PassesCommentThrough()
    {
        var id = Guid.NewGuid();
        _workflow.Setup(w => w.ExecuteTaskAsync(id, "reject", "bulk reject", _userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkflowTaskResult.Success());

        var result = await Handler().Handle(
            new BatchExecuteTasksCommand(new[] { id }, "reject", "bulk reject"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _workflow.Verify(w => w.ExecuteTaskAsync(id, "reject", "bulk reject", _userId, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
