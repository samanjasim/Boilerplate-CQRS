using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Domain.Enums;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

/// <summary>
/// End-to-end assertions over the <see cref="WorkflowTaskResult"/> shape
/// returned by <see cref="WorkflowEngine.ExecuteTaskAsync"/> for each failure
/// path. Complements <see cref="ExecuteTaskCommandHandlerTests"/> (which
/// covers the handler adapter) and <see cref="WorkflowEngineTests"/> (which
/// covers happy-path transitions) by pinning the exact code + kind + field
/// errors emitted by the engine.
/// </summary>
public sealed class ExecuteTaskResultShapeTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly WorkflowEngine _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _initiatorId = Guid.NewGuid();
    private readonly Guid _approverUserId = Guid.NewGuid();

    public ExecuteTaskResultShapeTests()
    {
        _db = WorkflowEngineTestFactory.CreateDb();
        _sut = WorkflowEngineTestFactory.Build(_db).Engine;
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ExecuteTaskAsync_TaskNotFound_ReturnsNotFoundShape()
    {
        var result = await _sut.ExecuteTaskAsync(
            Guid.NewGuid(), "approve", null, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("Workflow.TaskNotFound");
        result.Kind.Should().Be(WorkflowErrorKind.NotFound);
        result.FieldErrors.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteTaskAsync_TaskAlreadyCompleted_ReturnsConflictShape()
    {
        var (taskId, _) = await CreateSimpleApprovalTaskAsync();
        var task = await _db.ApprovalTasks.FirstAsync(t => t.Id == taskId);
        task.Complete("reject", comment: null, userId: _approverUserId);
        await _db.SaveChangesAsync();

        var result = await _sut.ExecuteTaskAsync(taskId, "approve", null, _approverUserId);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("Workflow.TaskNotPending");
        result.Kind.Should().Be(WorkflowErrorKind.Conflict);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WrongActor_ReturnsForbiddenShape()
    {
        var (taskId, _) = await CreateSimpleApprovalTaskAsync();
        var otherUser = Guid.NewGuid();

        var result = await _sut.ExecuteTaskAsync(taskId, "approve", null, otherUser);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("Workflow.TaskNotAssignedToUser");
        result.Kind.Should().Be(WorkflowErrorKind.Forbidden);
        result.ErrorDescription.Should().Contain("not assigned to user");
    }

    [Fact]
    public async Task ExecuteTaskAsync_InvalidTrigger_ReturnsValidationShape()
    {
        var (taskId, _) = await CreateSimpleApprovalTaskAsync();

        var result = await _sut.ExecuteTaskAsync(taskId, "bogusTrigger", null, _approverUserId);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("Workflow.InvalidTransition");
        result.Kind.Should().Be(WorkflowErrorKind.Validation);
    }

    [Fact]
    public async Task ExecuteTaskAsync_FormValidationFails_ReturnsValidationShapeWithFieldErrors()
    {
        var taskId = await CreateTaskWithRequiredNumberFieldAsync(fieldName: "amount", min: 0);

        var result = await _sut.ExecuteTaskAsync(
            taskId, "approve", null, _approverUserId,
            formData: new Dictionary<string, object> { ["amount"] = -5.0 });

        result.IsFailure.Should().BeTrue();
        result.Kind.Should().Be(WorkflowErrorKind.Validation);
        result.ErrorCode.Should().Be(WorkflowTaskResult.ValidationErrorCode);
        result.FieldErrors.Should().NotBeNull();
        result.FieldErrors!.Should().ContainKey("amount");
        result.FieldErrors!["amount"].Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteTaskAsync_IdempotentResubmit_ReturnsSuccessShape()
    {
        var (taskId, _) = await CreateSimpleApprovalTaskAsync();
        var task = await _db.ApprovalTasks.FirstAsync(t => t.Id == taskId);
        task.Complete("approve", comment: null, userId: _approverUserId);
        await _db.SaveChangesAsync();

        var result = await _sut.ExecuteTaskAsync(taskId, "approve", null, _approverUserId);

        result.IsSuccess.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.FieldErrors.Should().BeNull();
    }

    // ── Fixture helpers ──────────────────────────────────────────────────────

    private async Task<(Guid taskId, Guid instanceId)> CreateSimpleApprovalTaskAsync()
    {
        var states = new List<WorkflowStateConfig>
        {
            new("PendingApproval", "Pending Approval", "HumanTask",
                Assignee: new AssigneeConfig("SpecificUser",
                    new Dictionary<string, object> { ["userId"] = _approverUserId.ToString() }),
                Actions: new List<string> { "approve", "reject" }),
            new("Approved", "Approved", "Terminal"),
            new("Rejected", "Rejected", "Terminal"),
        };
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("PendingApproval", "Approved", "approve"),
            new("PendingApproval", "Rejected", "reject"),
        };

        var definition = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "ShapeFixture",
            displayName: "Shape Fixture",
            entityType: "Order",
            statesJson: JsonSerializer.Serialize(states),
            transitionsJson: JsonSerializer.Serialize(transitions),
            isTemplate: false,
            sourceModule: "Tests");
        _db.WorkflowDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        var entityId = Guid.NewGuid();
        var instance = WorkflowInstance.Create(
            tenantId: _tenantId,
            definitionId: definition.Id,
            entityType: "Order",
            entityId: entityId,
            initialState: "PendingApproval",
            startedByUserId: _initiatorId,
            contextJson: null,
            definitionName: "ShapeFixture",
            entityDisplayName: null);
        _db.WorkflowInstances.Add(instance);

        var task = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instance.Id,
            stepName: "PendingApproval",
            assigneeUserId: _approverUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            dueDate: null,
            entityType: "Order",
            entityId: entityId,
            definitionName: "ShapeFixture",
            definitionDisplayName: "Shape Fixture",
            entityDisplayName: null,
            formFieldsJson: null,
            availableActionsJson: "[]",
            slaReminderAfterHours: null);
        _db.ApprovalTasks.Add(task);
        await _db.SaveChangesAsync();
        return (task.Id, instance.Id);
    }

    private async Task<Guid> CreateTaskWithRequiredNumberFieldAsync(string fieldName, double min)
    {
        var formFields = new List<FormFieldDefinition>
        {
            new(Name: fieldName, Label: fieldName, Type: "number", Required: true, Min: min),
        };
        var states = new List<WorkflowStateConfig>
        {
            new("PendingApproval", "Pending Approval", "HumanTask",
                Assignee: new AssigneeConfig("SpecificUser",
                    new Dictionary<string, object> { ["userId"] = _approverUserId.ToString() }),
                Actions: new List<string> { "approve" },
                FormFields: formFields),
            new("Approved", "Approved", "Terminal"),
        };
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("PendingApproval", "Approved", "approve"),
        };

        var definition = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "FormShapeFixture",
            displayName: "Form Shape Fixture",
            entityType: "Order",
            statesJson: JsonSerializer.Serialize(states),
            transitionsJson: JsonSerializer.Serialize(transitions),
            isTemplate: false,
            sourceModule: "Tests");
        _db.WorkflowDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        var entityId = Guid.NewGuid();
        var instance = WorkflowInstance.Create(
            tenantId: _tenantId,
            definitionId: definition.Id,
            entityType: "Order",
            entityId: entityId,
            initialState: "PendingApproval",
            startedByUserId: _initiatorId,
            contextJson: null,
            definitionName: "FormShapeFixture",
            entityDisplayName: null);
        _db.WorkflowInstances.Add(instance);

        var task = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instance.Id,
            stepName: "PendingApproval",
            assigneeUserId: _approverUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            dueDate: null,
            entityType: "Order",
            entityId: entityId,
            definitionName: "FormShapeFixture",
            definitionDisplayName: "Form Shape Fixture",
            entityDisplayName: null,
            formFieldsJson: JsonSerializer.Serialize(formFields),
            availableActionsJson: "[]",
            slaReminderAfterHours: null);
        _db.ApprovalTasks.Add(task);
        await _db.SaveChangesAsync();

        return task.Id;
    }
}
