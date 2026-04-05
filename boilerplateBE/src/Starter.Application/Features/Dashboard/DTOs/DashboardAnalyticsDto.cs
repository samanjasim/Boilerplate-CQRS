namespace Starter.Application.Features.Dashboard.DTOs;

public sealed record DashboardAnalyticsDto(
    string Period,
    string[] EnabledSections,
    Dictionary<string, SummaryMetric> Summary,
    Dictionary<string, List<TimeSeriesPoint>> Charts);

public sealed record SummaryMetric(decimal Current, decimal Previous, decimal? Trend);

public sealed record TimeSeriesPoint(string Date, decimal Value);
