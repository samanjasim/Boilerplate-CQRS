using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Starter.Abstractions.Capabilities;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;
using TaskStatus = Starter.Module.Workflow.Domain.Enums.TaskStatus;

namespace Starter.Api.Tests.Workflow;

public sealed class SlaEscalationJobTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly Mock<IMessageDispatcher> _messageDispatcher = new();
    private readonly Mock<IUserReader> _userReader = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _dbName;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _initiatorId = Guid.NewGuid();
    private readonly Guid _assigneeId = Guid.NewGuid();
    private readonly Guid _fallbackAssigneeId = Guid.NewGuid();

    public SlaEscalationJobTests()
    {
        _dbName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _db = new WorkflowDbContext(options);

        // Build a minimal DI container for the scoped resolution pattern
        // Use the same database name so scoped contexts share the in-memory store
        var services = new ServiceCollection();
        services.AddScoped(sp =>
        {
            var opts = new DbContextOptionsBuilder<WorkflowDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;
            return new WorkflowDbContext(opts);
        });
        services.AddSingleton(_messageDispatcher.Object);

        // Set up a mock IRoleUserReader that returns the fallback assignee for "Admin" role
        var mockRoleUserReader = new Mock<IRoleUserReader>();
        mockRoleUserReader
            .Setup(r => r.GetUserIdsByRoleAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _fallbackAssigneeId });

        var builtInProvider = new BuiltInAssigneeProvider(mockRoleUserReader.Object);
        services.AddScoped<AssigneeResolverService>(sp =>
            new AssigneeResolverService(
                new IAssigneeResolverProvider[] { builtInProvider },
                _userReader.Object,
                NullLogger<AssigneeResolverService>.Instance));

        services.AddSingleton<IUserReader>(_userReader.Object);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var serviceProvider = services.BuildServiceProvider();
        _scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose() => _db.Dispose();

    private WorkflowDbContext CreateFreshContext()
    {
        var opts = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        return new WorkflowDbContext(opts);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private WorkflowDefinition SeedDefinitionWithSla(
        int? reminderAfterHours = null,
        int? escalateAfterHours = null)
    {
        var assigneeConfig = new AssigneeConfig(
            "SpecificUser",
            new Dictionary<string, object> { ["userId"] = _assigneeId.ToString() },
            Fallback: new AssigneeConfig("Role", new Dictionary<string, object> { ["roleName"] = "Admin" }));

        var states = new List<WorkflowStateConfig>
        {
            new("Draft", "Draft", "Initial"),
            new("PendingApproval", "Pending Approval", "HumanTask",
                Assignee: assigneeConfig,
                Actions: new List<string> { "approve", "reject" },
                Sla: new SlaConfig(reminderAfterHours, escalateAfterHours)),
            new("Approved", "Approved", "Terminal"),
        };

        var transitions = new List<WorkflowTransitionConfig>
        {
            new("Draft", "PendingApproval", "submit"),
            new("PendingApproval", "Approved", "approve"),
        };

        var definition = WorkflowDefinition.Create(
            _tenantId,
            "TestSlaWorkflow",
            "Test SLA Workflow",
            "Order",
            JsonSerializer.Serialize(states),
            JsonSerializer.Serialize(transitions),
            isTemplate: false,
            sourceModule: "Tests");

        _db.WorkflowDefinitions.Add(definition);
        _db.SaveChanges();
        return definition;
    }

    private WorkflowDefinition SeedDefinitionWithoutSla()
    {
        var states = new List<WorkflowStateConfig>
        {
            new("Draft", "Draft", "Initial"),
            new("PendingApproval", "Pending Approval", "HumanTask",
                Assignee: new AssigneeConfig("SpecificUser",
                    new Dictionary<string, object> { ["userId"] = _assigneeId.ToString() }),
                Actions: new List<string> { "approve" }),
            new("Approved", "Approved", "Terminal"),
        };

        var transitions = new List<WorkflowTransitionConfig>
        {
            new("Draft", "PendingApproval", "submit"),
            new("PendingApproval", "Approved", "approve"),
        };

        var definition = WorkflowDefinition.Create(
            _tenantId,
            "TestNoSlaWorkflow",
            "Test No SLA Workflow",
            "Order",
            JsonSerializer.Serialize(states),
            JsonSerializer.Serialize(transitions),
            isTemplate: false,
            sourceModule: "Tests");

        _db.WorkflowDefinitions.Add(definition);
        _db.SaveChanges();
        return definition;
    }

    private (WorkflowInstance Instance, ApprovalTask Task) SeedPendingTask(
        WorkflowDefinition definition,
        int hoursAgo,
        bool reminderAlreadySent = false,
        bool alreadyEscalated = false)
    {
        var instance = WorkflowInstance.Create(
            _tenantId,
            definition.Id,
            "Order",
            Guid.NewGuid(),
            "PendingApproval",
            _initiatorId,
            contextJson: null,
            definitionName: definition.DisplayName);

        _db.WorkflowInstances.Add(instance);
        _db.SaveChanges();

        var task = ApprovalTask.Create(
            _tenantId,
            instance.Id,
            "PendingApproval",
            _assigneeId,
            null,
            null,
            dueDate: null,
            entityType: "Order",
            entityId: instance.EntityId);

        _db.ApprovalTasks.Add(task);
        _db.SaveChanges();

        // Use reflection to set CreatedAt to the desired time in the past
        var createdAtProperty = typeof(ApprovalTask).BaseType!.BaseType!.BaseType!.BaseType!
            .GetProperty("CreatedAt")!;
        createdAtProperty.SetValue(task, DateTime.UtcNow.AddHours(-hoursAgo));

        if (reminderAlreadySent)
            task.MarkReminderSent();

        if (alreadyEscalated)
            task.MarkEscalated();

        _db.SaveChanges();

        return (instance, task);
    }

    private (WorkflowInstance Instance, ApprovalTask Task) SeedCompletedTask(
        WorkflowDefinition definition)
    {
        var instance = WorkflowInstance.Create(
            _tenantId,
            definition.Id,
            "Order",
            Guid.NewGuid(),
            "Approved",
            _initiatorId,
            contextJson: null,
            definitionName: definition.DisplayName);

        _db.WorkflowInstances.Add(instance);
        _db.SaveChanges();

        var task = ApprovalTask.Create(
            _tenantId,
            instance.Id,
            "PendingApproval",
            _assigneeId,
            null,
            null,
            dueDate: null,
            entityType: "Order",
            entityId: instance.EntityId);

        _db.ApprovalTasks.Add(task);
        _db.SaveChanges();

        task.Complete("approve", null, _assigneeId);
        _db.SaveChanges();

        return (instance, task);
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessOverdueTasks_TaskWithSlaReminder_SendsReminder()
    {
        // Arrange: task created 25h ago, reminder at 24h
        var definition = SeedDefinitionWithSla(reminderAfterHours: 24);
        var (_, task) = SeedPendingTask(definition, hoursAgo: 25);

        var sut = new SlaEscalationJob(_scopeFactory, NullLogger<SlaEscalationJob>.Instance);

        // Act
        await sut.ProcessOverdueTasksAsync(CancellationToken.None);

        // Assert
        _messageDispatcher.Verify(
            m => m.SendAsync(
                "workflow.sla-reminder",
                _assigneeId,
                It.IsAny<Dictionary<string, object>>(),
                _tenantId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify ReminderSentAt was set — use a fresh context to avoid stale tracked entities
        await using var verifyDb = CreateFreshContext();
        var updatedTask = await verifyDb.ApprovalTasks
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == task.Id);
        updatedTask.ReminderSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessOverdueTasks_TaskAlreadyReminded_DoesNotSendAgain()
    {
        // Arrange: task already has reminder sent
        var definition = SeedDefinitionWithSla(reminderAfterHours: 24);
        SeedPendingTask(definition, hoursAgo: 25, reminderAlreadySent: true);

        var sut = new SlaEscalationJob(_scopeFactory, NullLogger<SlaEscalationJob>.Instance);

        // Act
        await sut.ProcessOverdueTasksAsync(CancellationToken.None);

        // Assert: no reminder sent
        _messageDispatcher.Verify(
            m => m.SendAsync(
                "workflow.sla-reminder",
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOverdueTasks_TaskPastEscalation_Escalates()
    {
        // Arrange: task created 49h ago, escalate at 48h
        var definition = SeedDefinitionWithSla(escalateAfterHours: 48);
        var (_, task) = SeedPendingTask(definition, hoursAgo: 49);

        var sut = new SlaEscalationJob(_scopeFactory, NullLogger<SlaEscalationJob>.Instance);

        // Act
        await sut.ProcessOverdueTasksAsync(CancellationToken.None);

        // Assert: original task cancelled, new task created — use fresh context
        await using var verifyDb = CreateFreshContext();
        var originalTask = await verifyDb.ApprovalTasks
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == task.Id);
        originalTask.Status.Should().Be(TaskStatus.Cancelled);
        originalTask.EscalatedAt.Should().NotBeNull();

        // A new task should exist for the same instance
        var newTasks = await verifyDb.ApprovalTasks
            .IgnoreQueryFilters()
            .Where(t => t.InstanceId == task.InstanceId && t.Id != task.Id)
            .ToListAsync();
        newTasks.Should().HaveCount(1);
        newTasks[0].OriginalAssigneeUserId.Should().Be(_assigneeId);
        newTasks[0].Status.Should().Be(TaskStatus.Pending);

        // Escalation notification sent
        _messageDispatcher.Verify(
            m => m.SendAsync(
                "workflow.sla-escalated",
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, object>>(),
                _tenantId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOverdueTasks_TaskAlreadyEscalated_DoesNotEscalateAgain()
    {
        // Arrange: task already escalated
        var definition = SeedDefinitionWithSla(escalateAfterHours: 48);
        SeedPendingTask(definition, hoursAgo: 49, alreadyEscalated: true);

        var sut = new SlaEscalationJob(_scopeFactory, NullLogger<SlaEscalationJob>.Instance);

        // Act
        await sut.ProcessOverdueTasksAsync(CancellationToken.None);

        // Assert: no escalation notification sent
        _messageDispatcher.Verify(
            m => m.SendAsync(
                "workflow.sla-escalated",
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOverdueTasks_TaskWithNoSla_Skipped()
    {
        // Arrange: definition has no SLA config
        var definition = SeedDefinitionWithoutSla();
        SeedPendingTask(definition, hoursAgo: 100);

        var sut = new SlaEscalationJob(_scopeFactory, NullLogger<SlaEscalationJob>.Instance);

        // Act
        await sut.ProcessOverdueTasksAsync(CancellationToken.None);

        // Assert: no messages sent
        _messageDispatcher.Verify(
            m => m.SendAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOverdueTasks_CompletedTask_Skipped()
    {
        // Arrange: task is already completed
        var definition = SeedDefinitionWithSla(reminderAfterHours: 24);
        SeedCompletedTask(definition);

        var sut = new SlaEscalationJob(_scopeFactory, NullLogger<SlaEscalationJob>.Instance);

        // Act
        await sut.ProcessOverdueTasksAsync(CancellationToken.None);

        // Assert: no messages sent (completed tasks not queried)
        _messageDispatcher.Verify(
            m => m.SendAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
