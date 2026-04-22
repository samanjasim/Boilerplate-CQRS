using FluentAssertions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Workflow.Infrastructure;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class WorkflowOutboxRegistrationTests
{
    [Fact]
    public void WorkflowDbContext_Model_Includes_OutboxMessage_OutboxState_InboxState()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new WorkflowDbContext(options);

        var entityTypeNames = db.Model.GetEntityTypes()
            .Select(e => e.ClrType.Name)
            .ToList();

        entityTypeNames.Should().Contain(nameof(OutboxMessage));
        entityTypeNames.Should().Contain(nameof(OutboxState));
        entityTypeNames.Should().Contain(nameof(InboxState));
    }

    [Fact]
    public void AddWorkflowOutbox_Extension_Is_Callable_On_BusRegistrationConfigurator()
    {
        // Smoke test: the extension method exists and accepts an IBusRegistrationConfigurator.
        // We cannot build a full IBusRegistrationConfigurator outside an IServiceCollection,
        // but resolving the method via reflection guards against a refactor that drops it.
        var method = typeof(WorkflowMassTransitExtensions)
            .GetMethod(nameof(WorkflowMassTransitExtensions.AddWorkflowOutbox));

        method.Should().NotBeNull("WorkflowMassTransitExtensions.AddWorkflowOutbox is the public entry point used by Program.cs");
        method!.GetParameters().Single().ParameterType.Should().Be<IBusRegistrationConfigurator>();
    }
}
