using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class PendingTasksDenormalizationTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly WorkflowEngine _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public PendingTasksDenormalizationTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WorkflowDbContext(options);

        var userReader = new Mock<IUserReader>();
        var assigneeResolver = new AssigneeResolverService(
            new IAssigneeResolverProvider[] { new BuiltInAssigneeProvider(Mock.Of<IRoleUserReader>()) },
            userReader.Object,
            NullLogger<AssigneeResolverService>.Instance);

        var hookExecutor = new HookExecutor(
            Mock.Of<IMessageDispatcher>(),
            Mock.Of<IActivityService>(),
            Mock.Of<IWebhookPublisher>(),
            Mock.Of<INotificationServiceCapability>(),
            userReader.Object,
            new ConfigurationBuilder().Build(),
            NullLogger<HookExecutor>.Instance);

        var humanTaskFactory = new HumanTaskFactory(
            _db, assigneeResolver, NullLogger<HumanTaskFactory>.Instance);

        var conditionEvaluator = new ConditionEvaluator();
        var autoTransitionEvaluator = new AutoTransitionEvaluator(conditionEvaluator);

        var parallelCoordinator = new ParallelApprovalCoordinator(_db);

        _sut = new WorkflowEngine(
            _db,
            conditionEvaluator,
            assigneeResolver,
            hookExecutor,
            Mock.Of<ICommentService>(),
            userReader.Object,
            new FormDataValidator(),
            humanTaskFactory,
            autoTransitionEvaluator,
            parallelCoordinator,
            NullLogger<WorkflowEngine>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateTask_PopulatesDenormalizedFields()
    {
        // Arrange — definition with a HumanTask state that has form fields and an SLA reminder.
        var states = new List<WorkflowStateConfig>
        {
            new("Draft", "Draft", "Initial"),
            new("Review", "Review", "HumanTask",
                Assignee: new("SpecificUser", new() { ["userId"] = _userId.ToString() }),
                Actions: ["Approve", "Reject"],
                FormFields:
                [
                    new("amount", "Amount", "number", Required: true),
                ],
                Sla: new(ReminderAfterHours: 4, EscalateAfterHours: 8)),
        };
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("Draft", "Review", "Submit"),
            new("Review", "Draft", "Reject", Type: "Manual"),
            new("Review", "Draft", "Approve", Type: "Manual"),
        };

        var def = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "ExpenseFlow",
            displayName: "Expense Flow",
            entityType: "Expense",
            statesJson: JsonSerializer.Serialize(states),
            transitionsJson: JsonSerializer.Serialize(transitions),
            isTemplate: false,
            sourceModule: "Tests");
        _db.WorkflowDefinitions.Add(def);
        await _db.SaveChangesAsync();

        // Act — StartAsync seeds the instance in `Draft` (Initial), then HandleNewStateAsync
        // auto-transitions the first manual transition out of Initial (`Submit`), landing in
        // `Review` (HumanTask) which calls CreateApprovalTaskAsync. See WorkflowEngine.cs:1231-1239.
        var entityId = Guid.NewGuid();
        await _sut.StartAsync(
            entityType: "Expense",
            entityId: entityId,
            definitionName: "ExpenseFlow",
            initiatorUserId: _userId,
            tenantId: _tenantId,
            entityDisplayName: "Lunch with client");

        // Assert — task exists with all denormalized fields populated.
        var instance = await _db.WorkflowInstances.SingleAsync();
        instance.CurrentState.Should().Be("Review");

        var task = await _db.ApprovalTasks.SingleAsync();

        task.DefinitionName.Should().Be("ExpenseFlow");
        task.DefinitionDisplayName.Should().Be("Expense Flow");
        task.EntityType.Should().Be("Expense");
        task.EntityId.Should().Be(entityId);
        task.EntityDisplayName.Should().Be("Lunch with client");

        task.FormFieldsJson.Should().NotBeNullOrEmpty();
        var formFields = JsonSerializer.Deserialize<List<FormFieldDefinition>>(task.FormFieldsJson!);
        formFields.Should().ContainSingle().Which.Name.Should().Be("amount");

        var actions = JsonSerializer.Deserialize<List<string>>(task.AvailableActionsJson);
        actions.Should().BeEquivalentTo(new[] { "Approve", "Reject" });

        task.SlaReminderAfterHours.Should().Be(4);
    }

    [Fact]
    public async Task GetPendingTasks_FastPath_DoesNotRequireDefinitionRow()
    {
        // Arrange — seed an instance + definition + a fully denormalized task.
        var def = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "InboxFastPath",
            displayName: "Inbox Fast Path",
            entityType: "Order",
            statesJson: "[]",
            transitionsJson: "[]",
            isTemplate: false,
            sourceModule: "Tests");
        _db.WorkflowDefinitions.Add(def);

        var instance = WorkflowInstance.Create(
            tenantId: _tenantId,
            definitionId: def.Id,
            entityType: "Order",
            entityId: Guid.NewGuid(),
            initialState: "Review",
            startedByUserId: _userId,
            contextJson: null,
            definitionName: def.DisplayName);
        _db.WorkflowInstances.Add(instance);

        var task = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instance.Id,
            stepName: "Review",
            assigneeUserId: _userId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            entityType: "Order",
            entityId: instance.EntityId,
            definitionName: "InboxFastPath",
            availableActionsJson: "[\"Approve\",\"Reject\"]",
            definitionDisplayName: "Inbox Fast Path",
            entityDisplayName: "Order #42");
        _db.ApprovalTasks.Add(task);
        await _db.SaveChangesAsync();

        // Act — detach the task first (so EF won't cascade-delete it along the required FK),
        // then wipe the definition + instance so any JOIN would fail/return empty.
        _db.Entry(task).State = EntityState.Detached;
        _db.WorkflowInstances.Remove(instance);
        _db.WorkflowDefinitions.Remove(def);
        await _db.SaveChangesAsync();

        var page = await _sut.GetPendingTasksAsync(_userId, pageNumber: 1, pageSize: 10);

        // Assert — the task is still returned, populated from denormalized columns.
        page.Items.Should().HaveCount(1);
        var item = page.Items.Single();
        item.DefinitionName.Should().Be("InboxFastPath");
        item.EntityType.Should().Be("Order");
        item.EntityDisplayName.Should().Be("Order #42");
        item.AvailableActions.Should().BeEquivalentTo("Approve", "Reject");
    }

    [Fact]
    public async Task GetPendingTasks_LegacyTasks_FallbackToJoin()
    {
        // Arrange — seed a definition + instance + a task with EMPTY denormalized columns
        // (simulates a row that existed before the Phase 2b migration ran).
        var states = new List<WorkflowStateConfig>
        {
            new("Draft", "Draft", "Initial"),
            new("Review", "Review", "HumanTask",
                Actions: ["Approve", "Reject"],
                FormFields: [new("note", "Note", "text", Required: false)],
                Sla: new(ReminderAfterHours: 2)),
        };
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("Draft", "Review", "Submit"),
            new("Review", "Draft", "Approve", Type: "Manual"),
            new("Review", "Draft", "Reject", Type: "Manual"),
        };

        var def = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "LegacyFlow",
            displayName: "Legacy Flow",
            entityType: "Doc",
            statesJson: JsonSerializer.Serialize(states),
            transitionsJson: JsonSerializer.Serialize(transitions),
            isTemplate: false,
            sourceModule: "Tests");
        _db.WorkflowDefinitions.Add(def);

        var instance = WorkflowInstance.Create(
            tenantId: _tenantId,
            definitionId: def.Id,
            entityType: "Doc",
            entityId: Guid.NewGuid(),
            initialState: "Review",
            startedByUserId: _userId,
            contextJson: null,
            definitionName: def.DisplayName,
            entityDisplayName: "Q3 Roadmap");
        _db.WorkflowInstances.Add(instance);
        await _db.SaveChangesAsync();

        // Insert a task with a placeholder definitionName; we'll wipe it post-save to simulate legacy.
        var legacyTask = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instance.Id,
            stepName: "Review",
            assigneeUserId: _userId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            entityType: "Doc",
            entityId: instance.EntityId,
            definitionName: "PLACEHOLDER",
            availableActionsJson: "[]");
        _db.ApprovalTasks.Add(legacyTask);
        await _db.SaveChangesAsync();

        // Wipe the denormalized column to simulate a pre-Phase-2b row.
        var entry = _db.Entry(legacyTask);
        entry.Property(nameof(ApprovalTask.DefinitionName)).CurrentValue = string.Empty;
        await _db.SaveChangesAsync();

        // Act
        var page = await _sut.GetPendingTasksAsync(_userId, pageNumber: 1, pageSize: 10);

        // Assert — fallback fills in fields by joining Instance + Definition + StateConfig.
        page.Items.Should().HaveCount(1);
        var item = page.Items.Single();
        item.DefinitionName.Should().Be("LegacyFlow");
        item.EntityType.Should().Be("Doc");
        item.EntityDisplayName.Should().Be("Q3 Roadmap");
        item.AvailableActions.Should().BeEquivalentTo("Approve", "Reject");
        item.FormFields.Should().NotBeNull();
        item.FormFields!.Should().ContainSingle(f => f.Name == "note");
    }
}
