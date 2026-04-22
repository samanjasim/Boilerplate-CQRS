using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

/// <summary>
/// End-to-end verification that form data submitted with an approval action
/// is validated, merged into the instance context, and used to select a
/// conditional outgoing transition — proving the full
/// validate → merge → branch chain exercised by <c>ExecuteTaskAsync</c>.
/// </summary>
public sealed class FormDataBranchingIntegrationTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly WorkflowDbContext _db;
    private readonly WorkflowEngine _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _initiatorId = Guid.NewGuid();
    private readonly Guid _approverUserId = Guid.NewGuid();

    public FormDataBranchingIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);

        var userReader = new Mock<IUserReader>();
        var conditionEvaluator = new ConditionEvaluator();
        var builtInProvider = new BuiltInAssigneeProvider(Mock.Of<IRoleUserReader>());
        var assigneeResolver = new AssigneeResolverService(
            new IAssigneeResolverProvider[] { builtInProvider },
            userReader.Object,
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
    public async Task ExecuteTask_FormDataSatisfiesHighAmountCondition_BranchesToSeniorReview()
    {
        var states = BuildBranchingStates();
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("AwaitingApproval", "SeniorReview", "approve",
                Condition: new ConditionConfig(Field: "amount", Operator: "greaterThan", Value: 10000)),
            new("AwaitingApproval", "Approved", "approve"),
        };
        var (instanceId, taskId) = await SeedScenarioAsync(states, transitions);

        var result = await _sut.ExecuteTaskAsync(
            taskId, "approve", null, _approverUserId,
            formData: new Dictionary<string, object> { ["amount"] = 15000.0 },
            ct: default);

        result.IsSuccess.Should().BeTrue();

        var instance = await _db.WorkflowInstances.FindAsync(instanceId);
        instance!.CurrentState.Should().Be("SeniorReview");

        var ctx = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(instance.ContextJson!, JsonOpts);
        ctx!.Should().ContainKey("amount");
        ctx!["amount"].GetDouble().Should().Be(15000.0);
    }

    [Fact]
    public async Task ExecuteTask_FormDataBelowThreshold_BranchesToApproved()
    {
        var states = BuildBranchingStates();
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("AwaitingApproval", "SeniorReview", "approve",
                Condition: new ConditionConfig(Field: "amount", Operator: "greaterThan", Value: 10000)),
            new("AwaitingApproval", "Approved", "approve"),
        };
        var (instanceId, taskId) = await SeedScenarioAsync(states, transitions);

        var result = await _sut.ExecuteTaskAsync(
            taskId, "approve", null, _approverUserId,
            formData: new Dictionary<string, object> { ["amount"] = 500.0 },
            ct: default);

        result.IsSuccess.Should().BeTrue();
        var instance = await _db.WorkflowInstances.FindAsync(instanceId);
        instance!.CurrentState.Should().Be("Approved");

        var ctx = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(instance.ContextJson!, JsonOpts);
        ctx!["amount"].GetDouble().Should().Be(500.0);
    }

    [Fact]
    public async Task ExecuteTask_InvalidFormData_NoStateChangeAndNoContextMerge()
    {
        var states = new List<WorkflowStateConfig>
        {
            new("AwaitingApproval", "Awaiting Approval", "HumanTask",
                Assignee: new AssigneeConfig("SpecificUser",
                    new Dictionary<string, object> { ["userId"] = _approverUserId.ToString() }),
                Actions: new List<string> { "approve" },
                FormFields: new List<FormFieldDefinition>
                {
                    new("amount", "Amount", "number", Required: true, Min: 0),
                }),
            new("Approved", "Approved", "Terminal"),
        };
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("AwaitingApproval", "Approved", "approve"),
        };
        var (instanceId, taskId) = await SeedScenarioAsync(states, transitions);

        var result = await _sut.ExecuteTaskAsync(
            taskId, "approve", null, _approverUserId,
            formData: new Dictionary<string, object> { ["amount"] = -5.0 },
            ct: default);

        result.IsFailure.Should().BeTrue();
        result.Kind.Should().Be(WorkflowErrorKind.Validation);
        result.FieldErrors.Should().NotBeNull();
        result.FieldErrors!.Should().ContainKey("amount");

        var instance = await _db.WorkflowInstances.FindAsync(instanceId);
        instance!.CurrentState.Should().Be("AwaitingApproval");
        instance.ContextJson.Should().BeNull();
    }

    // ── Fixture helpers ──────────────────────────────────────────────────────

    private List<WorkflowStateConfig> BuildBranchingStates() => new()
    {
        new("AwaitingApproval", "Awaiting Approval", "HumanTask",
            Assignee: new AssigneeConfig("SpecificUser",
                new Dictionary<string, object> { ["userId"] = _approverUserId.ToString() }),
            Actions: new List<string> { "approve" },
            FormFields: new List<FormFieldDefinition>
            {
                new("amount", "Amount", "number", Required: true, Min: 0),
            }),
        new("SeniorReview", "Senior Review", "HumanTask",
            Assignee: new AssigneeConfig("SpecificUser",
                new Dictionary<string, object> { ["userId"] = _approverUserId.ToString() }),
            Actions: new List<string> { "approve" }),
        new("Approved", "Approved", "Terminal"),
    };

    private async Task<(Guid instanceId, Guid taskId)> SeedScenarioAsync(
        List<WorkflowStateConfig> states,
        List<WorkflowTransitionConfig> transitions)
    {
        var definition = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "BranchingFixture",
            displayName: "Branching Fixture",
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
            initialState: "AwaitingApproval",
            startedByUserId: _initiatorId,
            contextJson: null,
            definitionName: "BranchingFixture",
            entityDisplayName: null);
        _db.WorkflowInstances.Add(instance);

        var formFieldsJson = JsonSerializer.Serialize(states.First(s => s.Name == "AwaitingApproval").FormFields);

        var task = ApprovalTask.Create(
            tenantId: _tenantId,
            instanceId: instance.Id,
            stepName: "AwaitingApproval",
            assigneeUserId: _approverUserId,
            assigneeRole: null,
            assigneeStrategyJson: null,
            dueDate: null,
            entityType: "Order",
            entityId: entityId,
            definitionName: "BranchingFixture",
            definitionDisplayName: "Branching Fixture",
            entityDisplayName: null,
            formFieldsJson: formFieldsJson,
            availableActionsJson: "[\"approve\"]",
            slaReminderAfterHours: null);
        _db.ApprovalTasks.Add(task);
        await _db.SaveChangesAsync();

        return (instance.Id, task.Id);
    }
}
