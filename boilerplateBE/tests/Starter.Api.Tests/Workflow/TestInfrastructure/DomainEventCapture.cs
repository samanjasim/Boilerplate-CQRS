using Microsoft.EntityFrameworkCore.Diagnostics;
using Starter.Domain.Common;

namespace Starter.Api.Tests.Workflow.TestInfrastructure;

internal sealed class DomainEventCapture
{
    private readonly List<IDomainEvent> _events = [];
    public IReadOnlyList<IDomainEvent> Events => _events;
    public void Add(IEnumerable<IDomainEvent> events) => _events.AddRange(events);
}

internal sealed class DomainEventCaptureInterceptor(DomainEventCapture capture) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Capture(eventData);
        return base.SavingChanges(eventData, result);
    }

    private void Capture(DbContextEventData eventData)
    {
        if (eventData.Context is null) return;

        var aggregates = eventData.Context.ChangeTracker
            .Entries()
            .Select(e => e.Entity)
            .OfType<AggregateRoot>()
            .ToList();

        foreach (var aggregate in aggregates)
        {
            var events = aggregate.DomainEvents.ToArray();
            capture.Add(events);
        }
    }
}
