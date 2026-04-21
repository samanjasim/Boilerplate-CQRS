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

public sealed class GetPendingTasksPaginationTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly WorkflowEngine _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GetPendingTasksPaginationTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WorkflowDbContext(options);

        var userReader = new Mock<IUserReader>();
        var assigneeResolver = new AssigneeResolverService(
            new IAssigneeResolverProvider[]
            {
                new BuiltInAssigneeProvider(Mock.Of<IRoleUserReader>()),
            },
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

    private async Task<Guid> SeedDefinitionAndInstanceAsync()
    {
        var def = WorkflowDefinition.Create(
            tenantId: _tenantId,
            name: "PaginationTest",
            displayName: "Pagination Test",
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
            initialState: "PendingApproval",
            startedByUserId: _userId,
            contextJson: null,
            definitionName: def.Name);
        _db.WorkflowInstances.Add(instance);
        await _db.SaveChangesAsync();
        return instance.Id;
    }

    private async Task SeedTasksAsync(int count)
    {
        var instanceId = await SeedDefinitionAndInstanceAsync();

        for (int i = 0; i < count; i++)
        {
            var task = ApprovalTask.Create(
                tenantId: _tenantId,
                instanceId: instanceId,
                stepName: "PendingApproval",
                assigneeUserId: _userId,
                assigneeRole: null,
                assigneeStrategyJson: null,
                dueDate: null,
                entityType: "Order",
                entityId: Guid.NewGuid(),
                definitionName: "PaginationTest",
                definitionDisplayName: "Pagination Test",
                entityDisplayName: null,
                formFieldsJson: null,
                availableActionsJson: "[]",
                slaReminderAfterHours: null);
            _db.ApprovalTasks.Add(task);
        }
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetPendingTasksAsync_FirstPage_ReturnsRequestedPageSize()
    {
        await SeedTasksAsync(25);

        var page = await _sut.GetPendingTasksAsync(_userId, pageNumber: 1, pageSize: 10);

        page.Items.Should().HaveCount(10);
        page.TotalCount.Should().Be(25);
        page.PageNumber.Should().Be(1);
        page.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetPendingTasksAsync_SecondPage_ReturnsNextSlice()
    {
        await SeedTasksAsync(25);

        var page = await _sut.GetPendingTasksAsync(_userId, pageNumber: 2, pageSize: 10);

        page.Items.Should().HaveCount(10);
        page.TotalCount.Should().Be(25);
        page.PageNumber.Should().Be(2);
    }

    [Fact]
    public async Task GetPendingTasksAsync_LastPage_ReturnsRemainder()
    {
        await SeedTasksAsync(25);

        var page = await _sut.GetPendingTasksAsync(_userId, pageNumber: 3, pageSize: 10);

        page.Items.Should().HaveCount(5);
        page.TotalCount.Should().Be(25);
    }

    [Fact]
    public async Task GetPendingTasksAsync_EmptyResult_ReturnsZeroTotalCount()
    {
        var page = await _sut.GetPendingTasksAsync(_userId, pageNumber: 1, pageSize: 10);

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
        page.PageNumber.Should().Be(1);
        page.PageSize.Should().Be(10);
    }
}
