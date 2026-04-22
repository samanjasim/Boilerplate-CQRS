using FluentAssertions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.Commands.BatchExecuteTasks;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class BatchExecuteTasksTests
{
    private readonly Mock<IWorkflowService> _workflow = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Guid _userId = Guid.NewGuid();

    public BatchExecuteTasksTests()
    {
        _currentUser.SetupGet(x => x.UserId).Returns(_userId);
    }

    private BatchExecuteTasksCommandHandler Handler() => new(_workflow.Object, _currentUser.Object);

    [Fact]
    public async Task Handle_AllSucceed_ReturnsAllSucceeded()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _workflow
            .Setup(w => w.ExecuteTaskAsync(It.IsAny<Guid>(), "approve", null, _userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

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

        _workflow.Setup(w => w.ExecuteTaskAsync(success, "approve", null, _userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _workflow.Setup(w => w.ExecuteTaskAsync(fail, "approve", null, _userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
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
        r.Items.Single(i => i.TaskId == throws).Error.Should().Contain("nope");
    }

    [Fact]
    public async Task Handle_OneExceptionDoesNotAbortBatch()
    {
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();
        _workflow.Setup(w => w.ExecuteTaskAsync(bad, It.IsAny<string>(), null, _userId, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));
        _workflow.Setup(w => w.ExecuteTaskAsync(good, It.IsAny<string>(), null, _userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

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
            .ReturnsAsync(true);

        var result = await Handler().Handle(
            new BatchExecuteTasksCommand(new[] { id }, "reject", "bulk reject"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _workflow.Verify(w => w.ExecuteTaskAsync(id, "reject", "bulk reject", _userId, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
