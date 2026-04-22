using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Domain.Enums;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;
using TaskStatus = Starter.Module.Workflow.Domain.Enums.TaskStatus;

namespace Starter.Api.Tests.Workflow;

public sealed class WorkflowEngineTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly Mock<ICommentService> _commentService = new();
    private readonly Mock<IUserReader> _userReader = new();
    private readonly WorkflowEngine _sut;

    // Fixed IDs for deterministic tests
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _initiatorId = Guid.NewGuid();
    private readonly Guid _approverUserId = Guid.NewGuid();

    public WorkflowEngineTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WorkflowDbContext(options);

        var conditionEvaluator = new ConditionEvaluator();

        var builtInProvider = new BuiltInAssigneeProvider(
            Mock.Of<IRoleUserReader>());

        // Create a custom provider that resolves "SpecificUser" to our approver
        var assigneeResolver = new AssigneeResolverService(
            new IAssigneeResolverProvider[] { builtInProvider },
            _userReader.Object,
            NullLogger<AssigneeResolverService>.Instance);

        var hookExecutor = new HookExecutor(
            Mock.Of<IMessageDispatcher>(),
            Mock.Of<IActivityService>(),
            Mock.Of<IWebhookPublisher>(),
            Mock.Of<INotificationServiceCapability>(),
            NullLogger<HookExecutor>.Instance);

        var humanTaskFactory = new HumanTaskFactory(_db, assigneeResolver);

        var autoTransitionEvaluator = new AutoTransitionEvaluator(conditionEvaluator);

        var parallelCoordinator = new ParallelApprovalCoordinator(_db);

        _sut = new WorkflowEngine(
            _db,
            hookExecutor,
            _commentService.Object,
            _userReader.Object,
            new FormDataValidator(),
            humanTaskFactory,
            autoTransitionEvaluator,
            parallelCoordinator,
            NullLogger<WorkflowEngine>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a 3-state workflow: Draft (Initial) → PendingApproval (HumanTask) → Approved (Terminal) / Rejected (Terminal).
    /// Transitions: submit (Draft→PendingApproval), approve (PendingApproval→Approved), reject (PendingApproval→Rejected).
    /// The HumanTask state uses SpecificUser strategy pointing to _approverUserId.
    /// </summary>
    private async Task<WorkflowDefinition> SeedThreeStateDefinitionAsync(
        bool isActive = true,
        Guid? tenantId = null)
    {
        var states = new List<WorkflowStateConfig>
        {
            new("Draft", "Draft", "Initial"),
            new("PendingApproval", "Pending Approval", "HumanTask",
                Assignee: new AssigneeConfig("SpecificUser",
                    new Dictionary<string, object> { ["userId"] = _approverUserId.ToString() }),
                Actions: new List<string> { "approve", "reject" }),
            new("Approved", "Approved", "Terminal"),
            new("Rejected", "Rejected", "Terminal"),
        };

        var transitions = new List<WorkflowTransitionConfig>
        {
            new("Draft", "PendingApproval", "submit"),
            new("PendingApproval", "Approved", "approve"),
            new("PendingApproval", "Rejected", "reject"),
        };

        var statesJson = JsonSerializer.Serialize(states);
        var transitionsJson = JsonSerializer.Serialize(transitions);

        var definition = WorkflowDefinition.Create(
            tenantId: tenantId ?? _tenantId,
            name: "TestApproval",
            displayName: "Test Approval Workflow",
            entityType: "Order",
            statesJson: statesJson,
            transitionsJson: transitionsJson,
            isTemplate: false,
            sourceModule: "Tests");

        if (!isActive) definition.Deactivate();

        _db.WorkflowDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        return definition;
    }

    // ── 1. StartAsync — valid definition creates instance ────────────────────

    [Fact]
    public async Task StartAsync_ValidDefinition_CreatesInstance()
    {
        var def = await SeedThreeStateDefinitionAsync();

        var instanceId = await _sut.StartAsync(
            "Order", Guid.NewGuid(), "TestApproval",
            _initiatorId, _tenantId);

        instanceId.Should().NotBe(Guid.Empty);

        var instance = await _db.WorkflowInstances
            .FirstOrDefaultAsync(i => i.Id == instanceId);

        instance.Should().NotBeNull();
        // Initial state auto-transitions from Draft to PendingApproval
        instance!.CurrentState.Should().Be("PendingApproval");
        instance.Status.Should().Be(InstanceStatus.Active);
        instance.DefinitionId.Should().Be(def.Id);
    }

    // ── 2. StartAsync — initial HumanTask creates ApprovalTask ──────────────

    [Fact]
    public async Task StartAsync_InitialHumanTask_CreatesApprovalTask()
    {
        // Build a definition where the initial state IS a HumanTask
        var states = new List<WorkflowStateConfig>
        {
            new("Review", "Review", "HumanTask",
                Assignee: new AssigneeConfig("SpecificUser",
                    new Dictionary<string, object> { ["userId"] = _approverUserId.ToString() }),
                Actions: new List<string> { "approve" }),
            new("Done", "Done", "Terminal"),
        };

        var transitions = new List<WorkflowTransitionConfig>
        {
            new("Review", "Done", "approve"),
        };

        var definition = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "ImmediateReview",
            displayName: "Immediate Review",
            entityType: "Order",
            statesJson: JsonSerializer.Serialize(states),
            transitionsJson: JsonSerializer.Serialize(transitions),
            isTemplate: false,
            sourceModule: "Tests");

        _db.WorkflowDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        var instanceId = await _sut.StartAsync(
            "Order", Guid.NewGuid(), "ImmediateReview",
            _initiatorId, _tenantId);

        instanceId.Should().NotBe(Guid.Empty);

        var task = await _db.ApprovalTasks
            .FirstOrDefaultAsync(t => t.InstanceId == instanceId);

        task.Should().NotBeNull();
        task!.StepName.Should().Be("Review");
        task.AssigneeUserId.Should().Be(_approverUserId);
        task.Status.Should().Be(TaskStatus.Pending);
    }

    // ── 3. StartAsync — inactive definition returns Guid.Empty ──────────────

    [Fact]
    public async Task StartAsync_InactiveDefinition_ReturnsEmpty()
    {
        await SeedThreeStateDefinitionAsync(isActive: false);

        var instanceId = await _sut.StartAsync(
            "Order", Guid.NewGuid(), "TestApproval",
            _initiatorId, _tenantId);

        instanceId.Should().Be(Guid.Empty);
    }

    // ── 4. ExecuteTaskAsync — approve transitions to next state ──────────────

    [Fact]
    public async Task ExecuteTaskAsync_Approve_TransitionsToNextState()
    {
        await SeedThreeStateDefinitionAsync();
        var entityId = Guid.NewGuid();

        // Start workflow (lands on Draft)
        var instanceId = await _sut.StartAsync(
            "Order", entityId, "TestApproval", _initiatorId, _tenantId);

        // Manually transition to PendingApproval by creating the scenario:
        // The initial state is Draft (Initial type), so we need to submit first.
        // But submit is a manual action — we need a task for Draft state.
        // Since Draft is "Initial" (not HumanTask), there's no approval task for it.
        // We need to create a task manually or adjust the approach.
        // Actually — let's use the approach where Draft auto-transitions to PendingApproval.
        // Let me adjust: instead, the workflow starts at Draft, and the engine should
        // have created a step or we need to manually advance.

        // For this test, let's directly create an instance at PendingApproval with a task.
        var instance = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        instance.TransitionTo("PendingApproval", "submit", _initiatorId);

        var approvalTask = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instanceId,
            stepName: "PendingApproval",
            assigneeUserId: _approverUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            dueDate: null,
            entityType: "Order",
            entityId: entityId,
            definitionName: "TestApproval",
            definitionDisplayName: "Test Approval",
            entityDisplayName: null,
            formFieldsJson: null,
            availableActionsJson: "[]",
            slaReminderAfterHours: null);
        _db.ApprovalTasks.Add(approvalTask);
        await _db.SaveChangesAsync();

        // Execute approve
        var result = await _sut.ExecuteTaskAsync(
            approvalTask.Id, "approve", null, _approverUserId);

        result.Should().BeTrue();

        var updated = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        updated.CurrentState.Should().Be("Approved");
    }

    // ── 5. ExecuteTaskAsync — reject transitions to rejected state ───────────

    [Fact]
    public async Task ExecuteTaskAsync_Reject_TransitionsToRejectedState()
    {
        await SeedThreeStateDefinitionAsync();
        var entityId = Guid.NewGuid();

        var instanceId = await _sut.StartAsync(
            "Order", entityId, "TestApproval", _initiatorId, _tenantId);

        var instance = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        instance.TransitionTo("PendingApproval", "submit", _initiatorId);

        var approvalTask = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instanceId,
            stepName: "PendingApproval",
            assigneeUserId: _approverUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            dueDate: null,
            entityType: "Order",
            entityId: entityId,
            definitionName: "TestApproval",
            definitionDisplayName: "Test Approval",
            entityDisplayName: null,
            formFieldsJson: null,
            availableActionsJson: "[]",
            slaReminderAfterHours: null);
        _db.ApprovalTasks.Add(approvalTask);
        await _db.SaveChangesAsync();

        var result = await _sut.ExecuteTaskAsync(
            approvalTask.Id, "reject", "Not acceptable", _approverUserId);

        result.Should().BeTrue();

        var updated = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        updated.CurrentState.Should().Be("Rejected");
    }

    // ── 6. ExecuteTaskAsync — wrong assignee returns false ───────────────────

    [Fact]
    public async Task ExecuteTaskAsync_NotAssigned_ReturnsFalse()
    {
        await SeedThreeStateDefinitionAsync();
        var entityId = Guid.NewGuid();

        var instanceId = await _sut.StartAsync(
            "Order", entityId, "TestApproval", _initiatorId, _tenantId);

        var instance = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        instance.TransitionTo("PendingApproval", "submit", _initiatorId);

        var approvalTask = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instanceId,
            stepName: "PendingApproval",
            assigneeUserId: _approverUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            dueDate: null,
            entityType: "Order",
            entityId: entityId,
            definitionName: "TestApproval",
            definitionDisplayName: "Test Approval",
            entityDisplayName: null,
            formFieldsJson: null,
            availableActionsJson: "[]",
            slaReminderAfterHours: null);
        _db.ApprovalTasks.Add(approvalTask);
        await _db.SaveChangesAsync();

        var randomUserId = Guid.NewGuid();
        var result = await _sut.ExecuteTaskAsync(
            approvalTask.Id, "approve", null, randomUserId);

        result.Should().BeFalse();
    }

    // ── 7. ExecuteTaskAsync — terminal state completes instance ──────────────

    [Fact]
    public async Task ExecuteTaskAsync_TerminalState_CompletesInstance()
    {
        await SeedThreeStateDefinitionAsync();
        var entityId = Guid.NewGuid();

        var instanceId = await _sut.StartAsync(
            "Order", entityId, "TestApproval", _initiatorId, _tenantId);

        var instance = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        instance.TransitionTo("PendingApproval", "submit", _initiatorId);

        var approvalTask = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instanceId,
            stepName: "PendingApproval",
            assigneeUserId: _approverUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            dueDate: null,
            entityType: "Order",
            entityId: entityId,
            definitionName: "TestApproval",
            definitionDisplayName: "Test Approval",
            entityDisplayName: null,
            formFieldsJson: null,
            availableActionsJson: "[]",
            slaReminderAfterHours: null);
        _db.ApprovalTasks.Add(approvalTask);
        await _db.SaveChangesAsync();

        await _sut.ExecuteTaskAsync(
            approvalTask.Id, "approve", null, _approverUserId);

        var updated = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        updated.Status.Should().Be(InstanceStatus.Completed);
        updated.CompletedAt.Should().NotBeNull();
    }

    // ── 8. ExecuteTaskAsync — creates WorkflowStep record ────────────────────

    [Fact]
    public async Task ExecuteTaskAsync_CreatesWorkflowStep()
    {
        await SeedThreeStateDefinitionAsync();
        var entityId = Guid.NewGuid();

        var instanceId = await _sut.StartAsync(
            "Order", entityId, "TestApproval", _initiatorId, _tenantId);

        var instance = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        instance.TransitionTo("PendingApproval", "submit", _initiatorId);

        var approvalTask = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instanceId,
            stepName: "PendingApproval",
            assigneeUserId: _approverUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            dueDate: null,
            entityType: "Order",
            entityId: entityId,
            definitionName: "TestApproval",
            definitionDisplayName: "Test Approval",
            entityDisplayName: null,
            formFieldsJson: null,
            availableActionsJson: "[]",
            slaReminderAfterHours: null);
        _db.ApprovalTasks.Add(approvalTask);
        await _db.SaveChangesAsync();

        await _sut.ExecuteTaskAsync(
            approvalTask.Id, "approve", "Looks good", _approverUserId);

        var steps = await _db.WorkflowSteps
            .Where(s => s.InstanceId == instanceId)
            .ToListAsync();

        steps.Should().ContainSingle(s =>
            s.FromState == "PendingApproval"
            && s.ToState == "Approved"
            && s.Action == "approve"
            && s.ActorUserId == _approverUserId
            && s.Comment == "Looks good");
    }

    // ── 9. CancelAsync — cancels instance and pending tasks ──────────────────

    [Fact]
    public async Task CancelAsync_CancelsInstanceAndPendingTasks()
    {
        await SeedThreeStateDefinitionAsync();
        var entityId = Guid.NewGuid();

        var instanceId = await _sut.StartAsync(
            "Order", entityId, "TestApproval", _initiatorId, _tenantId);

        var instance = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        instance.TransitionTo("PendingApproval", "submit", _initiatorId);

        var approvalTask = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instanceId,
            stepName: "PendingApproval",
            assigneeUserId: _approverUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            dueDate: null,
            entityType: "Order",
            entityId: entityId,
            definitionName: "TestApproval",
            definitionDisplayName: "Test Approval",
            entityDisplayName: null,
            formFieldsJson: null,
            availableActionsJson: "[]",
            slaReminderAfterHours: null);
        _db.ApprovalTasks.Add(approvalTask);
        await _db.SaveChangesAsync();

        await _sut.CancelAsync(instanceId, "No longer needed", _initiatorId);

        var updated = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        updated.Status.Should().Be(InstanceStatus.Cancelled);

        var tasks = await _db.ApprovalTasks
            .Where(t => t.InstanceId == instanceId)
            .ToListAsync();
        tasks.Should().AllSatisfy(t => t.Status.Should().Be(TaskStatus.Cancelled));
    }

    // ── 10. GetStatusAsync — returns correct status ──────────────────────────

    [Fact]
    public async Task GetStatusAsync_ReturnsCorrectStatus()
    {
        var def = await SeedThreeStateDefinitionAsync();
        var entityId = Guid.NewGuid();

        var instanceId = await _sut.StartAsync(
            "Order", entityId, "TestApproval", _initiatorId, _tenantId);

        var status = await _sut.GetStatusAsync("Order", entityId);

        status.Should().NotBeNull();
        status!.InstanceId.Should().Be(instanceId);
        // Initial state auto-transitions from Draft → PendingApproval
        status.CurrentState.Should().Be("PendingApproval");
        status.Status.Should().Be("Active");
        status.DefinitionName.Should().Be("TestApproval");
    }

    // ── 11. GetPendingTasksAsync — returns only the user's tasks ─────────────

    [Fact]
    public async Task GetPendingTasksAsync_ReturnsOnlyUserTasks()
    {
        await SeedThreeStateDefinitionAsync();
        var entityId = Guid.NewGuid();

        // Start workflow — auto-transitions from Draft to PendingApproval,
        // creating an approval task for the Admin role (resolved to _approverUserId)
        var instanceId = await _sut.StartAsync(
            "Order", entityId, "TestApproval", _initiatorId, _tenantId);

        // Also create a pending task for a different user on the same instance
        var otherUserId = Guid.NewGuid();
        var otherTask = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instanceId,
            stepName: "PendingApproval",
            assigneeUserId: otherUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            dueDate: null,
            entityType: "Order",
            entityId: entityId,
            definitionName: "TestApproval",
            definitionDisplayName: "Test Approval",
            entityDisplayName: null,
            formFieldsJson: null,
            availableActionsJson: "[]",
            slaReminderAfterHours: null);
        _db.ApprovalTasks.Add(otherTask);
        await _db.SaveChangesAsync();

        var pending = await _sut.GetPendingTasksAsync(_approverUserId);

        // Should only see the auto-created task for the approver, not the other user's task
        pending.Items.Should().HaveCount(1);
        pending.TotalCount.Should().Be(1);
    }

    // ── 12. SeedTemplateAsync — creates definition ───────────────────────────

    [Fact]
    public async Task SeedTemplateAsync_CreatesDefinition()
    {
        var config = new WorkflowTemplateConfig(
            DisplayName: "Invoice Approval",
            Description: "Standard invoice approval flow",
            States: new List<WorkflowStateConfig>
            {
                new("Draft", "Draft", "Initial"),
                new("Review", "Review", "HumanTask"),
                new("Approved", "Approved", "Terminal"),
            },
            Transitions: new List<WorkflowTransitionConfig>
            {
                new("Draft", "Review", "submit"),
                new("Review", "Approved", "approve"),
            });

        await _sut.SeedTemplateAsync("InvoiceApproval", "Invoice", config);

        var def = await _db.WorkflowDefinitions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Name == "InvoiceApproval");

        def.Should().NotBeNull();
        def!.IsTemplate.Should().BeTrue();
        def.EntityType.Should().Be("Invoice");
        def.IsActive.Should().BeTrue();
    }

    // ── 13. SeedTemplateAsync — existing template is skipped ─────────────────

    [Fact]
    public async Task SeedTemplateAsync_ExistingTemplate_Skips()
    {
        var config = new WorkflowTemplateConfig(
            DisplayName: "Invoice Approval",
            Description: null,
            States: new List<WorkflowStateConfig>
            {
                new("Draft", "Draft", "Initial"),
                new("Approved", "Approved", "Terminal"),
            },
            Transitions: new List<WorkflowTransitionConfig>
            {
                new("Draft", "Approved", "approve"),
            });

        // Seed once
        await _sut.SeedTemplateAsync("InvoiceApproval", "Invoice", config);

        // Seed again — should not throw or create duplicate
        await _sut.SeedTemplateAsync("InvoiceApproval", "Invoice", config);

        var count = await _db.WorkflowDefinitions
            .IgnoreQueryFilters()
            .CountAsync(d => d.Name == "InvoiceApproval");

        count.Should().Be(1);
    }

    // ── Parallel Approval Helpers ───────────────────────────────────────────

    private readonly Guid _parallelUserA = Guid.NewGuid();
    private readonly Guid _parallelUserB = Guid.NewGuid();

    /// <summary>
    /// Seeds a definition with a parallel HumanTask state:
    /// Draft (Initial) → ParallelReview (HumanTask with Parallel config) → Approved / Rejected (Terminal).
    /// </summary>
    private async Task<WorkflowDefinition> SeedParallelDefinitionAsync(string mode = "AllOf")
    {
        var states = new List<WorkflowStateConfig>
        {
            new("Draft", "Draft", "Initial"),
            new("ParallelReview", "Parallel Review", "HumanTask",
                Parallel: new ParallelConfig(mode, new List<AssigneeConfig>
                {
                    new("SpecificUser", new Dictionary<string, object> { ["userId"] = _parallelUserA.ToString() }),
                    new("SpecificUser", new Dictionary<string, object> { ["userId"] = _parallelUserB.ToString() }),
                }),
                Actions: new List<string> { "approve", "reject" }),
            new("Approved", "Approved", "Terminal"),
            new("Rejected", "Rejected", "Terminal"),
        };

        var transitions = new List<WorkflowTransitionConfig>
        {
            new("Draft", "ParallelReview", "submit"),
            new("ParallelReview", "Approved", "approve"),
            new("ParallelReview", "Rejected", "reject"),
        };

        var definition = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "ParallelApproval",
            displayName: "Parallel Approval Workflow",
            entityType: "Order",
            statesJson: JsonSerializer.Serialize(states),
            transitionsJson: JsonSerializer.Serialize(transitions),
            isTemplate: false,
            sourceModule: "Tests");

        _db.WorkflowDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        return definition;
    }

    // ── 14. Parallel AllOf — creates multiple tasks with shared GroupId ──────

    [Fact]
    public async Task StartAsync_ParallelAllOf_CreatesMultipleTasks()
    {
        await SeedParallelDefinitionAsync("AllOf");
        var entityId = Guid.NewGuid();

        var instanceId = await _sut.StartAsync(
            "Order", entityId, "ParallelApproval", _initiatorId, _tenantId);

        instanceId.Should().NotBe(Guid.Empty);

        var instance = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        instance.CurrentState.Should().Be("ParallelReview");

        var tasks = await _db.ApprovalTasks
            .Where(t => t.InstanceId == instanceId)
            .ToListAsync();

        tasks.Should().HaveCount(2);
        tasks.Should().AllSatisfy(t => t.Status.Should().Be(TaskStatus.Pending));
        tasks.Should().AllSatisfy(t => t.GroupId.Should().NotBeNull());

        // All tasks share the same GroupId
        var groupIds = tasks.Select(t => t.GroupId).Distinct().ToList();
        groupIds.Should().HaveCount(1);

        // Each task has a different assignee
        var assigneeIds = tasks.Select(t => t.AssigneeUserId).ToList();
        assigneeIds.Should().Contain(_parallelUserA);
        assigneeIds.Should().Contain(_parallelUserB);
    }

    // ── 15. Parallel AllOf — partial approval waits ─────────────────────────

    [Fact]
    public async Task ExecuteTaskAsync_ParallelAllOf_PartialApproval_Waits()
    {
        await SeedParallelDefinitionAsync("AllOf");
        var entityId = Guid.NewGuid();

        var instanceId = await _sut.StartAsync(
            "Order", entityId, "ParallelApproval", _initiatorId, _tenantId);

        var tasks = await _db.ApprovalTasks
            .Where(t => t.InstanceId == instanceId)
            .ToListAsync();

        var taskA = tasks.First(t => t.AssigneeUserId == _parallelUserA);
        var taskB = tasks.First(t => t.AssigneeUserId == _parallelUserB);

        // Complete only one task
        var result = await _sut.ExecuteTaskAsync(
            taskA.Id, "approve", null, _parallelUserA);

        result.Should().BeTrue();

        // Workflow should still be Active at ParallelReview
        var instance = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        instance.CurrentState.Should().Be("ParallelReview");
        instance.Status.Should().Be(InstanceStatus.Active);

        // Completed task is done, other is still pending
        var updatedTaskA = await _db.ApprovalTasks.FirstAsync(t => t.Id == taskA.Id);
        updatedTaskA.Status.Should().Be(TaskStatus.Completed);

        var updatedTaskB = await _db.ApprovalTasks.FirstAsync(t => t.Id == taskB.Id);
        updatedTaskB.Status.Should().Be(TaskStatus.Pending);
    }

    // ── 16. Parallel AllOf — last approval transitions ──────────────────────

    [Fact]
    public async Task ExecuteTaskAsync_ParallelAllOf_LastApproval_Transitions()
    {
        await SeedParallelDefinitionAsync("AllOf");
        var entityId = Guid.NewGuid();

        var instanceId = await _sut.StartAsync(
            "Order", entityId, "ParallelApproval", _initiatorId, _tenantId);

        var tasks = await _db.ApprovalTasks
            .Where(t => t.InstanceId == instanceId)
            .ToListAsync();

        var taskA = tasks.First(t => t.AssigneeUserId == _parallelUserA);
        var taskB = tasks.First(t => t.AssigneeUserId == _parallelUserB);

        // Complete first task
        await _sut.ExecuteTaskAsync(taskA.Id, "approve", null, _parallelUserA);

        // Complete second task — should trigger transition
        var result = await _sut.ExecuteTaskAsync(
            taskB.Id, "approve", null, _parallelUserB);

        result.Should().BeTrue();

        var instance = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        instance.CurrentState.Should().Be("Approved");
        instance.Status.Should().Be(InstanceStatus.Completed);
    }

    // ── 17. Parallel AllOf — any reject cancels all and transitions ─────────

    [Fact]
    public async Task ExecuteTaskAsync_ParallelAllOf_AnyReject_CancelsAllAndTransitions()
    {
        await SeedParallelDefinitionAsync("AllOf");
        var entityId = Guid.NewGuid();

        var instanceId = await _sut.StartAsync(
            "Order", entityId, "ParallelApproval", _initiatorId, _tenantId);

        var tasks = await _db.ApprovalTasks
            .Where(t => t.InstanceId == instanceId)
            .ToListAsync();

        var taskA = tasks.First(t => t.AssigneeUserId == _parallelUserA);
        var taskB = tasks.First(t => t.AssigneeUserId == _parallelUserB);

        // Reject from one user
        var result = await _sut.ExecuteTaskAsync(
            taskA.Id, "reject", "Not approved", _parallelUserA);

        result.Should().BeTrue();

        // Workflow transitions to Rejected
        var instance = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        instance.CurrentState.Should().Be("Rejected");
        instance.Status.Should().Be(InstanceStatus.Completed);

        // The other task should be cancelled
        var updatedTaskB = await _db.ApprovalTasks.FirstAsync(t => t.Id == taskB.Id);
        updatedTaskB.Status.Should().Be(TaskStatus.Cancelled);
    }

    // ── 18. Parallel AnyOf — first approval transitions and cancels rest ────

    [Fact]
    public async Task ExecuteTaskAsync_ParallelAnyOf_FirstApproval_TransitionsAndCancelsRest()
    {
        await SeedParallelDefinitionAsync("AnyOf");
        var entityId = Guid.NewGuid();

        var instanceId = await _sut.StartAsync(
            "Order", entityId, "ParallelApproval", _initiatorId, _tenantId);

        var tasks = await _db.ApprovalTasks
            .Where(t => t.InstanceId == instanceId)
            .ToListAsync();

        tasks.Should().HaveCount(2);

        var taskA = tasks.First(t => t.AssigneeUserId == _parallelUserA);
        var taskB = tasks.First(t => t.AssigneeUserId == _parallelUserB);

        // Complete one task — should immediately transition
        var result = await _sut.ExecuteTaskAsync(
            taskA.Id, "approve", null, _parallelUserA);

        result.Should().BeTrue();

        // Workflow transitions to Approved
        var instance = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        instance.CurrentState.Should().Be("Approved");
        instance.Status.Should().Be(InstanceStatus.Completed);

        // The other task should be cancelled
        var updatedTaskB = await _db.ApprovalTasks.FirstAsync(t => t.Id == taskB.Id);
        updatedTaskB.Status.Should().Be(TaskStatus.Cancelled);
    }

    // ── 19. Single assignee — no GroupId, works unchanged ───────────────────

    [Fact]
    public async Task SingleAssignee_NoGroupId_WorksUnchanged()
    {
        await SeedThreeStateDefinitionAsync();
        var entityId = Guid.NewGuid();

        var instanceId = await _sut.StartAsync(
            "Order", entityId, "TestApproval", _initiatorId, _tenantId);

        // Should have a single task with no GroupId
        var tasks = await _db.ApprovalTasks
            .Where(t => t.InstanceId == instanceId)
            .ToListAsync();

        tasks.Should().HaveCount(1);
        tasks[0].GroupId.Should().BeNull();
        tasks[0].AssigneeUserId.Should().Be(_approverUserId);

        // Complete the single task — should transition normally
        var result = await _sut.ExecuteTaskAsync(
            tasks[0].Id, "approve", null, _approverUserId);

        result.Should().BeTrue();

        var instance = await _db.WorkflowInstances.FirstAsync(i => i.Id == instanceId);
        instance.CurrentState.Should().Be("Approved");
        instance.Status.Should().Be(InstanceStatus.Completed);
    }
}
