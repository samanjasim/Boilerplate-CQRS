using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Readers;
using Starter.Module.Workflow.Domain.Errors;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;

internal sealed class GetWorkflowAnalyticsQueryHandler(
    WorkflowDbContext db,
    IUserReader userReader)
    : IRequestHandler<GetWorkflowAnalyticsQuery, Result<WorkflowAnalyticsDto>>
{
    private readonly IUserReader _userReader = userReader;

    public async Task<Result<WorkflowAnalyticsDto>> Handle(
        GetWorkflowAnalyticsQuery request, CancellationToken ct)
    {
        var definition = await db.WorkflowDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DefinitionId, ct);

        if (definition is null)
            return Result.Failure<WorkflowAnalyticsDto>(
                WorkflowErrors.DefinitionNotFoundById(request.DefinitionId));

        if (definition.IsTemplate)
            return Result.Failure<WorkflowAnalyticsDto>(
                WorkflowErrors.AnalyticsNotAvailableOnTemplate());

        var now = DateTime.UtcNow;
        var (windowStart, windowEnd) = ResolveWindow(request.Window, definition.CreatedAt, now);

        var headline = await ComputeHeadlineAsync(definition.Id, windowStart, windowEnd, ct);

        var series = await ComputeInstanceCountSeriesAsync(
            definition.Id, request.Window, windowStart, windowEnd, ct);

        var dto = new WorkflowAnalyticsDto(
            DefinitionId: definition.Id,
            DefinitionName: definition.Name,
            Window: request.Window,
            WindowStart: windowStart,
            WindowEnd: windowEnd,
            InstancesInWindow: headline.TotalStarted,
            Headline: headline,
            StatesByBottleneck: Array.Empty<StateMetric>(),
            ActionRates: Array.Empty<ActionRateMetric>(),
            InstanceCountSeries: series,
            StuckInstances: Array.Empty<StuckInstanceDto>(),
            ApproverActivity: Array.Empty<ApproverActivityDto>());

        return Result.Success(dto);
    }

    private static (DateTime Start, DateTime End) ResolveWindow(
        WindowSelector window, DateTime definitionCreatedAt, DateTime now) =>
        window switch
        {
            WindowSelector.SevenDays   => (now.AddDays(-7),  now),
            WindowSelector.ThirtyDays  => (now.AddDays(-30), now),
            WindowSelector.NinetyDays  => (now.AddDays(-90), now),
            WindowSelector.AllTime     => (definitionCreatedAt, now),
            _ => throw new ArgumentOutOfRangeException(nameof(window)),
        };

    private async Task<IReadOnlyList<InstanceCountPoint>> ComputeInstanceCountSeriesAsync(
        Guid definitionId,
        WindowSelector window,
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken ct)
    {
        var granularity = PickGranularity(window);

        // Always materialize within-window rows and bucket in C# so EF InMemory
        // (tests) and Postgres behave identically. This is the read path's hot
        // aggregation — profiling in the perf test (Task 13) is the signal to
        // move to raw SQL date_trunc if 1s is ever breached.
        var rows = await db.WorkflowInstances
            .AsNoTracking()
            .Where(i => i.DefinitionId == definitionId
                     && i.StartedAt >= windowStart
                     && i.StartedAt <= windowEnd)
            .Select(i => new
            {
                i.StartedAt,
                i.Status,
                i.CompletedAt,
                i.CancelledAt,
            })
            .ToListAsync(ct);

        var dict = new Dictionary<DateTime, (int started, int completed, int cancelled)>();
        for (var cursor = TruncateTo(windowStart, granularity);
             cursor <= TruncateTo(windowEnd, granularity);
             cursor = Advance(cursor, granularity))
        {
            dict[cursor] = (0, 0, 0);
        }

        foreach (var r in rows)
        {
            var startBucket = TruncateTo(r.StartedAt, granularity);
            if (dict.TryGetValue(startBucket, out var s))
                dict[startBucket] = (s.started + 1, s.completed, s.cancelled);

            if (r.Status == Domain.Enums.InstanceStatus.Completed && r.CompletedAt.HasValue)
            {
                var completedBucket = TruncateTo(r.CompletedAt.Value, granularity);
                if (dict.TryGetValue(completedBucket, out var c))
                    dict[completedBucket] = (c.started, c.completed + 1, c.cancelled);
            }

            if (r.Status == Domain.Enums.InstanceStatus.Cancelled && r.CancelledAt.HasValue)
            {
                var cancelledBucket = TruncateTo(r.CancelledAt.Value, granularity);
                if (dict.TryGetValue(cancelledBucket, out var x))
                    dict[cancelledBucket] = (x.started, x.completed, x.cancelled + 1);
            }
        }

        return dict
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new InstanceCountPoint(kvp.Key, kvp.Value.started, kvp.Value.completed, kvp.Value.cancelled))
            .ToList();
    }

    private static BucketGranularity PickGranularity(WindowSelector window) => window switch
    {
        WindowSelector.SevenDays  => BucketGranularity.Day,
        WindowSelector.ThirtyDays => BucketGranularity.Day,
        WindowSelector.NinetyDays => BucketGranularity.Week,
        WindowSelector.AllTime    => BucketGranularity.Month,
        _ => BucketGranularity.Day,
    };

    private static DateTime TruncateTo(DateTime dt, BucketGranularity g) => g switch
    {
        BucketGranularity.Day   => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc),
        BucketGranularity.Week  => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc)
                                     .AddDays(-(int)dt.DayOfWeek),
        BucketGranularity.Month => new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, DateTimeKind.Utc),
        _ => dt,
    };

    private static DateTime Advance(DateTime dt, BucketGranularity g) => g switch
    {
        BucketGranularity.Day   => dt.AddDays(1),
        BucketGranularity.Week  => dt.AddDays(7),
        BucketGranularity.Month => dt.AddMonths(1),
        _ => dt.AddDays(1),
    };

    private async Task<HeadlineMetrics> ComputeHeadlineAsync(
        Guid definitionId, DateTime windowStart, DateTime windowEnd, CancellationToken ct)
    {
        var rows = await db.WorkflowInstances
            .AsNoTracking()
            .Where(i => i.DefinitionId == definitionId
                     && i.StartedAt >= windowStart
                     && i.StartedAt <= windowEnd)
            .Select(i => new
            {
                i.Status,
                i.StartedAt,
                i.CompletedAt,
            })
            .ToListAsync(ct);

        var total = rows.Count;
        var completed = rows.Count(r => r.Status == Domain.Enums.InstanceStatus.Completed);
        var cancelled = rows.Count(r => r.Status == Domain.Enums.InstanceStatus.Cancelled);

        double? avgCycleHours = null;
        var completedWithCycle = rows
            .Where(r => r.Status == Domain.Enums.InstanceStatus.Completed && r.CompletedAt.HasValue)
            .Select(r => (r.CompletedAt!.Value - r.StartedAt).TotalHours)
            .ToList();
        if (completedWithCycle.Count > 0)
            avgCycleHours = Math.Round(completedWithCycle.Average(), 1);

        return new HeadlineMetrics(total, completed, cancelled, avgCycleHours);
    }

    private enum BucketGranularity { Day, Week, Month }
}
