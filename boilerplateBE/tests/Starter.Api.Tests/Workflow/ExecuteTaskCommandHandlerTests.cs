using FluentAssertions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Application.Commands.ExecuteTask;
using Starter.Shared.Results;
using Xunit;

namespace Starter.Api.Tests.Workflow;

/// <summary>
/// Verifies that <see cref="ExecuteTaskCommandHandler"/> adapts
/// <see cref="WorkflowTaskResult"/> back into <see cref="Result{T}"/> with the
/// correct shape, so <see cref="WorkflowErrorKind"/> propagates cleanly to
/// <see cref="ErrorType"/> and the API emits the right HTTP status (not a
/// generic 500) for every failure path.
/// </summary>
public sealed class ExecuteTaskCommandHandlerTests
{
    private readonly Mock<IWorkflowService> _workflow = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Guid _userId = Guid.NewGuid();

    public ExecuteTaskCommandHandlerTests()
    {
        _currentUser.SetupGet(x => x.UserId).Returns(_userId);
    }

    private ExecuteTaskCommandHandler Handler() => new(_workflow.Object, _currentUser.Object);

    [Fact]
    public async Task Handle_Success_ReturnsSuccessTrue()
    {
        var taskId = Guid.NewGuid();
        _workflow.Setup(w => w.ExecuteTaskAsync(taskId, "approve", null, _userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkflowTaskResult.Success());

        var result = await Handler().Handle(
            new ExecuteTaskCommand(taskId, "approve", null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidationFailure_ReturnsResultWithValidationErrors()
    {
        var taskId = Guid.NewGuid();
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["amount"] = ["Amount must be greater than 0"],
            ["notes"] = ["Notes is required", "Notes exceeds max length"],
        };
        _workflow.Setup(w => w.ExecuteTaskAsync(taskId, "approve", null, _userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkflowTaskResult.ValidationFailure(fieldErrors));

        var result = await Handler().Handle(
            new ExecuteTaskCommand(taskId, "approve", null, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.ValidationErrors.Should().NotBeNull();
        var dict = result.ValidationErrors!.ToDictionary();
        dict.Should().ContainKey("amount");
        dict["amount"].Should().ContainSingle()
            .Which.Should().Be("Amount must be greater than 0");
        dict.Should().ContainKey("notes");
        dict["notes"].Should().HaveCount(2);
    }

    [Theory]
    [InlineData(WorkflowErrorKind.NotFound, ErrorType.NotFound, "Workflow.TaskNotFound")]
    [InlineData(WorkflowErrorKind.Forbidden, ErrorType.Forbidden, "Workflow.TaskNotAssignedToUser")]
    [InlineData(WorkflowErrorKind.Validation, ErrorType.Validation, "Workflow.InvalidTransition")]
    [InlineData(WorkflowErrorKind.Conflict, ErrorType.Conflict, "Workflow.TaskNotPending")]
    [InlineData(WorkflowErrorKind.Conflict, ErrorType.Conflict, "Workflow.Concurrency")]
    [InlineData(WorkflowErrorKind.Failure, ErrorType.Failure, "Workflow.SomeUnknownFutureCode")]
    [InlineData(WorkflowErrorKind.Unauthorized, ErrorType.Unauthorized, "Workflow.NotAuthenticated")]
    public async Task Handle_Failure_MapsKindToErrorType(
        WorkflowErrorKind kind, ErrorType expectedType, string code)
    {
        var taskId = Guid.NewGuid();
        _workflow.Setup(w => w.ExecuteTaskAsync(taskId, "approve", null, _userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkflowTaskResult.Failure(code, "boom", kind));

        var result = await Handler().Handle(
            new ExecuteTaskCommand(taskId, "approve", null, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(code);
        result.Error.Description.Should().Be("boom");
        result.Error.Type.Should().Be(expectedType);
    }
}
