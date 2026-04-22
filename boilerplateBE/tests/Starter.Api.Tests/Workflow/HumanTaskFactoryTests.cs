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

public sealed class HumanTaskFactoryTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly HumanTaskFactory _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _initiatorId = Guid.NewGuid();
    private readonly Guid _approverId = Guid.NewGuid();

    public HumanTaskFactoryTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);

        var builtIn = new BuiltInAssigneeProvider(Mock.Of<IRoleUserReader>());
        var resolver = new AssigneeResolverService(
            new IAssigneeResolverProvider[] { builtIn },
            Mock.Of<IUserReader>(),
            NullLogger<AssigneeResolverService>.Instance);

        _sut = new HumanTaskFactory(_db, resolver);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateAsync_SingleAssignee_PopulatesDenormalizedColumns()
    {
        var (instance, definition, state) = SeedInstanceAndState(
            stateType: "HumanTask",
            assignee: new AssigneeConfig("SpecificUser",
                new Dictionary<string, object> { ["userId"] = _approverId.ToString() }),
            actions: new List<string> { "approve", "reject" });

        await _sut.CreateAsync(instance, state, definition, _initiatorId, CancellationToken.None);
        await _db.SaveChangesAsync();

        var task = await _db.ApprovalTasks.SingleAsync();
        task.AssigneeUserId.Should().Be(_approverId);
        task.StepName.Should().Be(state.Name);
        task.DefinitionName.Should().Be(definition.Name);
        task.DefinitionDisplayName.Should().Be(definition.DisplayName);
        task.EntityType.Should().Be(instance.EntityType);
        task.EntityId.Should().Be(instance.EntityId);
        task.AvailableActionsJson.Should().Contain("approve").And.Contain("reject");
        task.GroupId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_Parallel_AllOf_CreatesOneTaskPerAssigneeWithSharedGroupId()
    {
        var (instance, definition, state) = SeedInstanceAndState(
            stateType: "HumanTask",
            parallel: new ParallelConfig("AllOf", new List<AssigneeConfig>
            {
                new("SpecificUser", new Dictionary<string, object> { ["userId"] = _approverId.ToString() }),
                new("SpecificUser", new Dictionary<string, object> { ["userId"] = Guid.NewGuid().ToString() }),
            }),
            actions: new List<string> { "approve", "reject" });

        await _sut.CreateAsync(instance, state, definition, _initiatorId, CancellationToken.None);
        await _db.SaveChangesAsync();

        var tasks = await _db.ApprovalTasks.ToListAsync();
        tasks.Should().HaveCount(2);
        tasks.Select(t => t.GroupId).Distinct().Should().HaveCount(1, "both tasks share a group");
        tasks.Select(t => t.GroupId!.Value).All(g => g != Guid.Empty).Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithFormFields_SerializesFormFieldsJson()
    {
        var fields = new List<FormFieldDefinition>
        {
            new("amount", "Amount", "number", Required: true, Min: 0, Max: 10000),
        };
        var (instance, definition, state) = SeedInstanceAndState(
            stateType: "HumanTask",
            assignee: new AssigneeConfig("SpecificUser",
                new Dictionary<string, object> { ["userId"] = _approverId.ToString() }),
            actions: new List<string> { "approve" },
            formFields: fields);

        await _sut.CreateAsync(instance, state, definition, _initiatorId, CancellationToken.None);
        await _db.SaveChangesAsync();

        var task = await _db.ApprovalTasks.SingleAsync();
        task.FormFieldsJson.Should().NotBeNullOrEmpty();
        task.FormFieldsJson!.Should().Contain("amount").And.Contain("number");
    }

    [Fact]
    public async Task CreateAsync_WithSla_CapturesReminderHours()
    {
        var (instance, definition, state) = SeedInstanceAndState(
            stateType: "HumanTask",
            assignee: new AssigneeConfig("SpecificUser",
                new Dictionary<string, object> { ["userId"] = _approverId.ToString() }),
            actions: new List<string> { "approve" },
            sla: new SlaConfig(ReminderAfterHours: 4, EscalateAfterHours: 8));

        await _sut.CreateAsync(instance, state, definition, _initiatorId, CancellationToken.None);
        await _db.SaveChangesAsync();

        var task = await _db.ApprovalTasks.SingleAsync();
        task.SlaReminderAfterHours.Should().Be(4);
    }

    private (WorkflowInstance, WorkflowDefinition, WorkflowStateConfig) SeedInstanceAndState(
        string stateType,
        AssigneeConfig? assignee = null,
        ParallelConfig? parallel = null,
        List<string>? actions = null,
        SlaConfig? sla = null,
        List<FormFieldDefinition>? formFields = null)
    {
        var state = new WorkflowStateConfig(
            Name: "Review",
            DisplayName: "Review",
            Type: stateType,
            Assignee: assignee,
            Actions: actions,
            Parallel: parallel,
            Sla: sla,
            FormFields: formFields);
        var transitions = new List<WorkflowTransitionConfig>
        {
            new("Review", "Done", "approve"),
            new("Review", "Rejected", "reject"),
        };
        var definition = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "TestDef",
            displayName: "Test Definition",
            entityType: "Order",
            statesJson: JsonSerializer.Serialize(new[] { state }),
            transitionsJson: JsonSerializer.Serialize(transitions),
            isTemplate: false,
            sourceModule: "Tests");
        _db.WorkflowDefinitions.Add(definition);

        var instance = WorkflowInstance.Create(
            tenantId: _tenantId,
            definitionId: definition.Id,
            entityType: "Order",
            entityId: Guid.NewGuid(),
            initialState: state.Name,
            startedByUserId: _initiatorId,
            contextJson: null,
            definitionName: definition.DisplayName,
            entityDisplayName: "Order #1");
        _db.WorkflowInstances.Add(instance);
        _db.SaveChanges();

        return (instance, definition, state);
    }
}
