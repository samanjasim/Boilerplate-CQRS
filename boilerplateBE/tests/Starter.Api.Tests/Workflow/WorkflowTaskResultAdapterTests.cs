using FluentAssertions;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Application.Common;
using Starter.Shared.Results;
using Xunit;

namespace Starter.Api.Tests.Workflow;

/// <summary>
/// Pins the <see cref="WorkflowTaskResultAdapter"/> enum-mapping contract so
/// re-ordering either <see cref="WorkflowErrorKind"/> or <see cref="ErrorType"/>
/// cannot silently mis-translate an error. The exhaustiveness test enumerates
/// every <see cref="WorkflowErrorKind"/> value at runtime — adding a new kind
/// without updating the adapter forces this test to fail.
/// </summary>
public sealed class WorkflowTaskResultAdapterTests
{
    [Theory]
    [InlineData(WorkflowErrorKind.Failure, ErrorType.Failure)]
    [InlineData(WorkflowErrorKind.Validation, ErrorType.Validation)]
    [InlineData(WorkflowErrorKind.NotFound, ErrorType.NotFound)]
    [InlineData(WorkflowErrorKind.Conflict, ErrorType.Conflict)]
    [InlineData(WorkflowErrorKind.Unauthorized, ErrorType.Unauthorized)]
    [InlineData(WorkflowErrorKind.Forbidden, ErrorType.Forbidden)]
    public void ToErrorType_MapsKnownKinds(WorkflowErrorKind kind, ErrorType expected)
    {
        WorkflowTaskResultAdapter.ToErrorType(kind).Should().Be(expected);
    }

    [Fact]
    public void ToErrorType_None_Throws()
    {
        Action act = () => WorkflowTaskResultAdapter.ToErrorType(WorkflowErrorKind.None);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*None is only valid on Success*");
    }

    [Fact]
    public void ToErrorType_CoversEveryKind_ExceptNone()
    {
        // Runtime exhaustiveness guard: if a new WorkflowErrorKind is added
        // without updating the adapter, this test fails. None is excluded
        // because it represents "no error" and is not valid input for the
        // mapping.
        foreach (var kind in Enum.GetValues<WorkflowErrorKind>())
        {
            if (kind == WorkflowErrorKind.None) continue;
            var act = () => WorkflowTaskResultAdapter.ToErrorType(kind);
            act.Should().NotThrow($"WorkflowErrorKind.{kind} must be handled by the adapter");
        }
    }

    [Fact]
    public void ToResult_Success_ReturnsSuccessTrue()
    {
        var result = WorkflowTaskResultAdapter.ToResult(WorkflowTaskResult.Success());
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void ToResult_ValidationFailure_PopulatesValidationErrors()
    {
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["amount"] = ["must be >= 0"],
        };
        var wfResult = WorkflowTaskResult.ValidationFailure(fieldErrors);

        var result = WorkflowTaskResultAdapter.ToResult(wfResult);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors!.ToDictionary().Should().ContainKey("amount");
    }

    [Fact]
    public void ToResult_TypedFailure_PreservesCodeDescriptionAndType()
    {
        var wfResult = WorkflowTaskResult.Failure(
            "Workflow.TaskNotFound", "Task was not found", WorkflowErrorKind.NotFound);

        var result = WorkflowTaskResultAdapter.ToResult(wfResult);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Workflow.TaskNotFound");
        result.Error.Description.Should().Be("Task was not found");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
