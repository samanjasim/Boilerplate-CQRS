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

        var bottlenecks = await ComputeBottlenecksAsync(definition.Id, windowStart, windowEnd, ct);

        var actionRates = await ComputeActionRatesAsync(definition.Id, windowStart, windowEnd, ct);

        var stuckInstances = await ComputeStuckInstancesAsync(definition.Id, windowStart, windowEnd, now, ct);

        var approverActivity = await ComputeApproverActivityAsync(definition.Id, windowStart, windowEnd, ct);

        var dto = new WorkflowAnalyticsDto(
            DefinitionId: definition.Id,
            DefinitionName: definition.Name,
            Window: request.Window,
            WindowStart: windowStart,
            WindowEnd: windowEnd,
            InstancesInWindow: headline.TotalStarted,
            Headline: headline,
            StatesByBottleneck: bottlenecks,
            ActionRates: actionRates,
            InstanceCountSeries: series,
            StuckInstances: stuckInstances,
            ApproverActivity: approverActivity);

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

    private async Task<IReadOnlyList<StateMetric>> ComputeBottlenecksAsync(
        Guid definitionId, DateTime windowStart, DateTime windowEnd, CancellationToken ct)
    {
        var instanceIds = await db.WorkflowInstances
            .AsNoTracking()
            .Where(i => i.DefinitionId == definitionId
                     && i.StartedAt >= windowStart
                     && i.StartedAt <= windowEnd)
            .Select(i => i.Id)
            .ToListAsync(ct);

        if (instanceIds.Count == 0) return Array.Empty<StateMetric>();

        var steps = await db.WorkflowSteps
            .AsNoTracking()
            .Where(s => instanceIds.Contains(s.InstanceId))
            .OrderBy(s => s.InstanceId).ThenBy(s => s.Timestamp)
            .Select(s => new { s.InstanceId, s.FromState, s.ToState, s.Timestamp })
            .ToListAsync(ct);

        var dwellsByState = new Dictionary<string, List<double>>(StringComparer.Ordinal);

        foreach (var group in steps.GroupBy(s => s.InstanceId))
        {
            var ordered = group.ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var entry = ordered[i];
                for (var j = i + 1; j < ordered.Count; j++)
                {
                    var exit = ordered[j];
                    if (exit.FromState == entry.ToState)
                    {
                        var hours = (exit.Timestamp - entry.Timestamp).TotalHours;
                        if (!dwellsByState.TryGetValue(entry.ToState, out var list))
                        {
                            list = new List<double>();
                            dwellsByState[entry.ToState] = list;
                        }
                        list.Add(hours);
                        break;
                    }
                }
            }
        }

        return dwellsByState
            .Where(kvp => kvp.Value.Count >= 3)
            .Select(kvp => new StateMetric(
                StateName: kvp.Key,
                MedianDwellHours: Math.Round(Percentile(kvp.Value, 0.5), 2),
                P95DwellHours: Math.Round(Percentile(kvp.Value, 0.95), 2),
                VisitCount: kvp.Value.Count))
            .OrderByDescending(m => m.MedianDwellHours)
            .ToList();
    }

    private static double Percentile(List<double> values, double quantile)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        var rank = quantile * (sorted.Count - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper) return sorted[lower];
        var weight = rank - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }

    private async Task<IReadOnlyList<ActionRateMetric>> ComputeActionRatesAsync(
        Guid definitionId, DateTime windowStart, DateTime windowEnd, CancellationToken ct)
    {
        var instanceIds = await db.WorkflowInstances
            .AsNoTracking()
            .Where(i => i.DefinitionId == definitionId
                     && i.StartedAt >= windowStart
                     && i.StartedAt <= windowEnd)
            .Select(i => i.Id)
            .ToListAsync(ct);

        if (instanceIds.Count == 0) return Array.Empty<ActionRateMetric>();

        var steps = await db.WorkflowSteps
            .AsNoTracking()
            .Where(s => instanceIds.Contains(s.InstanceId)
                     && s.StepType == Domain.Enums.StepType.HumanTask
                     && s.ActorUserId != null)
            .Select(s => new { s.FromState, s.Action })
            .ToListAsync(ct);

        return steps
            .GroupBy(s => s.FromState)
            .SelectMany(stateGroup =>
            {
                var totalInState = stateGroup.Count();
                return stateGroup
                    .GroupBy(s => s.Action)
                    .Select(actionGroup => new ActionRateMetric(
                        StateName: stateGroup.Key,
                        Action: actionGroup.Key,
                        Count: actionGroup.Count(),
                        Percentage: Math.Round((double)actionGroup.Count() / totalInState, 4)));
            })
            .OrderBy(m => m.StateName).ThenByDescending(m => m.Count)
            .ToList();
    }

    private async Task<IReadOnlyList<StuckInstanceDto>> ComputeStuckInstancesAsync(
        Guid definitionId, DateTime windowStart, DateTime windowEnd, DateTime now, CancellationToken ct)
    {
        var activeRows = await db.WorkflowInstances
            .AsNoTracking()
            .Where(i => i.DefinitionId == definitionId
                     && i.Status == Domain.Enums.InstanceStatus.Active
                     && i.StartedAt >= windowStart
                     && i.StartedAt <= windowEnd)
            .OrderBy(i => i.StartedAt)
            .Take(10)
            .Select(i => new
            {
                i.Id,
                i.EntityDisplayName,
                i.CurrentState,
                i.StartedAt,
            })
            .ToListAsync(ct);

        if (activeRows.Count == 0) return Array.Empty<StuckInstanceDto>();

        var instanceIdList = activeRows.Select(r => r.Id).ToList();

        var pendingAssigneesByInstance = await db.ApprovalTasks
            .AsNoTracking()
            .Where(t => instanceIdList.Contains(t.InstanceId)
                     && t.Status == Domain.Enums.TaskStatus.Pending
                     && t.AssigneeUserId != null)
            .GroupBy(t => t.InstanceId)
            .Select(g => new { InstanceId = g.Key, AssigneeUserId = g.First().AssigneeUserId!.Value })
            .ToListAsync(ct);

        var assigneeLookup = pendingAssigneesByInstance
            .ToDictionary(x => x.InstanceId, x => x.AssigneeUserId);

        var userIds = assigneeLookup.Values.Distinct().ToList();
        var displayNameLookup = new Dictionary<Guid, string>();
        if (userIds.Count > 0)
        {
            var users = await _userReader.GetManyAsync(userIds, ct);
            foreach (var u in users) displayNameLookup[u.Id] = u.DisplayName;
        }

        return activeRows.Select(r =>
        {
            string? assigneeName = null;
            if (assigneeLookup.TryGetValue(r.Id, out var uid)
                && displayNameLookup.TryGetValue(uid, out var name))
                assigneeName = name;

            return new StuckInstanceDto(
                InstanceId: r.Id,
                EntityDisplayName: r.EntityDisplayName,
                CurrentState: r.CurrentState,
                StartedAt: r.StartedAt,
                DaysSinceStarted: (int)Math.Ceiling((now - r.StartedAt).TotalDays),
                CurrentAssigneeDisplayName: assigneeName);
        }).ToList();
    }

    private async Task<IReadOnlyList<ApproverActivityDto>> ComputeApproverActivityAsync(
        Guid definitionId, DateTime windowStart, DateTime windowEnd, CancellationToken ct)
    {
        var instanceIds = await db.WorkflowInstances
            .AsNoTracking()
            .Where(i => i.DefinitionId == definitionId
                     && i.StartedAt >= windowStart
                     && i.StartedAt <= windowEnd)
            .Select(i => i.Id)
            .ToListAsync(ct);

        if (instanceIds.Count == 0) return Array.Empty<ApproverActivityDto>();

        var steps = await db.WorkflowSteps
            .AsNoTracking()
            .Where(s => instanceIds.Contains(s.InstanceId)
                     && s.StepType == Domain.Enums.StepType.HumanTask
                     && s.ActorUserId != null)
            .Select(s => new { ActorUserId = s.ActorUserId!.Value, s.Action, s.Timestamp })
            .ToListAsync(ct);

        if (steps.Count == 0) return Array.Empty<ApproverActivityDto>();

        var completedTasks = await db.ApprovalTasks
            .AsNoTracking()
            .Where(t => instanceIds.Contains(t.InstanceId)
                     && t.Status == Domain.Enums.TaskStatus.Completed
                     && t.CompletedByUserId != null
                     && t.CompletedAt != null)
            .Select(t => new
            {
                UserId = t.CompletedByUserId!.Value,
                CompletedAt = t.CompletedAt!.Value,
                t.CreatedAt,
            })
            .ToListAsync(ct);

        var grouped = steps
            .GroupBy(s => s.ActorUserId)
            .Select(g =>
            {
                var stepList = g.ToList();
                var approvals  = stepList.Count(x => string.Equals(x.Action, "approve",           StringComparison.OrdinalIgnoreCase));
                var rejections = stepList.Count(x => string.Equals(x.Action, "reject",            StringComparison.OrdinalIgnoreCase));
                var returns    = stepList.Count(x =>
                    string.Equals(x.Action, "return",            StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Action, "returnforrevision", StringComparison.OrdinalIgnoreCase));
                var userId = g.Key;

                var samples = new List<double>();
                foreach (var step in stepList)
                {
                    var matchedTask = completedTasks.FirstOrDefault(t =>
                        t.UserId == userId &&
                        Math.Abs((t.CompletedAt - step.Timestamp).TotalSeconds) < 1.0);
                    if (matchedTask != null)
                        samples.Add((matchedTask.CompletedAt - matchedTask.CreatedAt).TotalHours);
                }

                return new
                {
                    UserId     = userId,
                    Approvals  = approvals,
                    Rejections = rejections,
                    Returns    = returns,
                    Total      = stepList.Count,
                    AvgHours   = samples.Count > 0 ? (double?)Math.Round(samples.Average(), 2) : null,
                };
            })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToList();

        var userIds = grouped.Select(x => x.UserId).Distinct().ToList();
        var displayNameLookup = new Dictionary<Guid, string>();
        if (userIds.Count > 0)
        {
            var users = await _userReader.GetManyAsync(userIds, ct);
            foreach (var u in users) displayNameLookup[u.Id] = u.DisplayName;
        }

        return grouped.Select(x => new ApproverActivityDto(
            UserId: x.UserId,
            UserDisplayName: displayNameLookup.TryGetValue(x.UserId, out var name) ? name : x.UserId.ToString(),
            Approvals: x.Approvals,
            Rejections: x.Rejections,
            Returns: x.Returns,
            AvgResponseTimeHours: x.AvgHours)).ToList();
    }

    private enum BucketGranularity { Day, Week, Month }
}
