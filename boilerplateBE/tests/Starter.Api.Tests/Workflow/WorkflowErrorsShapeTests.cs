using FluentAssertions;
using Starter.Module.Workflow.Application.Common;
using Starter.Module.Workflow.Domain.Errors;
using Starter.Shared.Results;
using Xunit;

namespace Starter.Api.Tests.Workflow;

/// <summary>
/// Pins the <see cref="Error"/> shape and adapter translation for workflow
/// errors that are hard to exercise end-to-end — notably the concurrency
/// branch, which EF Core InMemory cannot trigger via the normal save path
/// because it does not enforce optimistic-concurrency tokens. Protecting the
/// shape here means a rename, code-change, or kind re-classification is
/// caught even when no integration test currently reaches that path.
/// </summary>
public sealed class WorkflowErrorsShapeTests
{
    [Fact]
    public void Concurrency_HasStableCodeAndConflictKind()
    {
        var error = WorkflowErrors.Concurrency();

        error.Code.Should().Be("Workflow.Concurrency");
        error.Type.Should().Be(ErrorType.Conflict);
        error.Description.Should().NotBeNullOrWhiteSpace();
        error.Description.Should().Contain("refresh", "operator-facing text guides the user to retry");
    }

    [Fact]
    public void TaskNotPending_HasStableCodeAndConflictKind()
    {
        var error = WorkflowErrors.TaskNotPending(Guid.NewGuid());

        error.Code.Should().Be("Workflow.TaskNotPending");
        error.Type.Should().Be(ErrorType.Conflict);
        error.Description.Should().Contain("pending");
    }

    [Fact]
    public void AdapterTranslatesConcurrencyFailure_ToConflictResult()
    {
        var wfResult = Starter.Abstractions.Capabilities.WorkflowTaskResult.Failure(
            WorkflowErrors.Concurrency().Code,
            WorkflowErrors.Concurrency().Description,
            Starter.Abstractions.Capabilities.WorkflowErrorKind.Conflict);

        var result = WorkflowTaskResultAdapter.ToResult(wfResult);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Workflow.Concurrency");
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }
}
