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

        _sut = new WorkflowEngine(
            _db,
            new ConditionEvaluator(),
            assigneeResolver,
            hookExecutor,
            Mock.Of<ICommentService>(),
            userReader.Object,
            new FormDataValidator(),
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
}
