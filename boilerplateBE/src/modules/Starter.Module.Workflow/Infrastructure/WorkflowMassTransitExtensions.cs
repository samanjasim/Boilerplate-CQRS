using MassTransit;
using Starter.Module.Workflow.Infrastructure.Persistence;

namespace Starter.Module.Workflow.Infrastructure;

public static class WorkflowMassTransitExtensions
{
    /// <summary>
    /// Registers a transactional EF outbox against <see cref="WorkflowDbContext"/>.
    /// With <c>UseBusOutbox()</c>, <c>IPublishEndpoint.Publish</c> and
    /// <c>ISendEndpoint.Send</c> calls made while <c>WorkflowDbContext</c> is the
    /// active DbContext are queued in the workflow outbox table and committed in
    /// the same transaction as <c>WorkflowDbContext.SaveChanges</c>. MassTransit's
    /// background delivery service then drains the outbox to the broker.
    ///
    /// The Workflow module deliberately opts out of the shared core outbox on
    /// <c>ApplicationDbContext</c> so that workflow events remain atomic with
    /// workflow state changes. See the outbox comment in
    /// <c>ApplicationDbContext.OnModelCreating</c> for the broader policy.
    /// Bare <c>IBus.Publish</c> calls bypass the outbox by design and should be
    /// avoided inside workflow handlers.
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
