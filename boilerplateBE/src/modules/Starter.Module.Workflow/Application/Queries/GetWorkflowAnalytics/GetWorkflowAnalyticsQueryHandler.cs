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

        var instancesInWindow = await db.WorkflowInstances
            .AsNoTracking()
            .CountAsync(i => i.DefinitionId == definition.Id
                          && i.StartedAt >= windowStart
                          && i.StartedAt <= windowEnd, ct);

        var headline = await ComputeHeadlineAsync(definition.Id, windowStart, windowEnd, ct);

        var series = BuildZeroFilledSeries(request.Window, windowStart, windowEnd);

        var dto = new WorkflowAnalyticsDto(
            DefinitionId: definition.Id,
            DefinitionName: definition.Name,
            Window: request.Window,
            WindowStart: windowStart,
            WindowEnd: windowEnd,
            InstancesInWindow: instancesInWindow,
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
            WindowSelector.AllTime     => (DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), now),
            _ => throw new ArgumentOutOfRangeException(nameof(window)),
        };

    private static IReadOnlyList<InstanceCountPoint> BuildZeroFilledSeries(
        WindowSelector window, DateTime start, DateTime end)
    {
        var granularity = PickGranularity(window);
        var buckets = new List<InstanceCountPoint>();
        var cursor = TruncateTo(start, granularity);
        var endTrunc = TruncateTo(end, granularity);

        while (cursor <= endTrunc)
        {
            buckets.Add(new InstanceCountPoint(cursor, 0, 0, 0));
            cursor = Advance(cursor, granularity);
        }

        return buckets;
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
