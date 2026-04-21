using MassTransit;
using Starter.Module.Workflow.Infrastructure.Persistence;

namespace Starter.Module.Workflow.Infrastructure;

public static class WorkflowMassTransitExtensions
{
    /// <summary>
    /// Registers a transactional EF outbox against <see cref="WorkflowDbContext"/>.
    /// Every <c>IBus.Publish</c> call made in a scope where <c>WorkflowDbContext</c>
    /// is the active DbContext is queued in the workflow outbox table within the
    /// same database transaction as the workflow state change. MassTransit's
    /// background delivery service then drains the outbox.
    /// </summary>
    public static IBusRegistrationConfigurator AddWorkflowOutbox(this IBusRegistrationConfigurator bus)
    {
        bus.AddEntityFrameworkOutbox<WorkflowDbContext>(o =>
        {
            o.QueryDelay = TimeSpan.FromSeconds(1);
            o.UsePostgres();
            o.UseBusOutbox();
        });
        return bus;
    }
}
