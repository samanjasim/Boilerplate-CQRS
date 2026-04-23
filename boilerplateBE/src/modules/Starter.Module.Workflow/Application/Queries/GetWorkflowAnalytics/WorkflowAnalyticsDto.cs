namespace Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;

public sealed record WorkflowAnalyticsDto(
    Guid DefinitionId,
    string DefinitionName,
    WindowSelector Window,
    DateTime WindowStart,
    DateTime WindowEnd,
    int InstancesInWindow,
    HeadlineMetrics Headline,
    IReadOnlyList<StateMetric> StatesByBottleneck,
    IReadOnlyList<ActionRateMetric> ActionRates,
    IReadOnlyList<InstanceCountPoint> InstanceCountSeries,
    IReadOnlyList<StuckInstanceDto> StuckInstances,
    IReadOnlyList<ApproverActivityDto> ApproverActivity);

public sealed record HeadlineMetrics(
    int TotalStarted,
    int TotalCompleted,
    int TotalCancelled,
    double? AvgCycleTimeHours);

public sealed record StateMetric(
    string StateName,
    double MedianDwellHours,
    double P95DwellHours,
    int VisitCount);

public sealed record ActionRateMetric(
    string StateName,
    string Action,
    int Count,
    double Percentage);

public sealed record InstanceCountPoint(
    DateTime Bucket,
    int Started,
    int Completed,
    int Cancelled);

public sealed record StuckInstanceDto(
    Guid InstanceId,
    string? EntityDisplayName,
    string CurrentState,
    DateTime StartedAt,
    int DaysSinceStarted,
    string? CurrentAssigneeDisplayName);

public sealed record ApproverActivityDto(
    Guid UserId,
    string UserDisplayName,
    int Approvals,
    int Rejections,
    int Returns,
    double? AvgResponseTimeHours);
