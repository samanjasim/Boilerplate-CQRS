using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Dashboard.DTOs;
using Starter.Domain.Billing.Enums;
using Starter.Domain.Common.Enums;
using Starter.Shared.Results;

namespace Starter.Application.Features.Dashboard.Queries.GetDashboardAnalytics;

internal sealed class GetDashboardAnalyticsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IFeatureFlagService flags,
    ICacheService cacheService,
    ILogger<GetDashboardAnalyticsQueryHandler> logger)
    : IRequestHandler<GetDashboardAnalyticsQuery, Result<DashboardAnalyticsDto>>
{
    public async Task<Result<DashboardAnalyticsDto>> Handle(
        GetDashboardAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"analytics:{currentUser.TenantId?.ToString() ?? "platform"}:{request.Period}";
        var cached = await cacheService.GetAsync<DashboardAnalyticsDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return Result.Success(cached);

        var (periodStart, prevStart) = ParsePeriod(request.Period);
        var isMonthly = request.Period == "12m";
        var isPlatformAdmin = currentUser.TenantId is null;

        // Determine enabled sections
        var sections = new List<string> { "users", "loginActivity", "activityBreakdown" };

        if (await flags.GetValueAsync<int>("files.max_storage_mb", cancellationToken) > 0)
            sections.Add("storage");
        if (await flags.IsEnabledAsync("api_keys.enabled", cancellationToken))
            sections.Add("apiKeys");
        if (await flags.IsEnabledAsync("reports.enabled", cancellationToken))
            sections.Add("reports");
        if (await flags.IsEnabledAsync("webhooks.enabled", cancellationToken))
            sections.Add("webhooks");
        if (await flags.IsEnabledAsync("imports.enabled", cancellationToken))
            sections.Add("imports");
        if (isPlatformAdmin)
        {
            sections.Add("revenue");
            sections.Add("tenants");
        }

        var summary = new Dictionary<string, SummaryMetric>();
        var charts = new Dictionary<string, List<TimeSeriesPoint>>();

        // Users
        var userResult = await ComputeUserMetrics(periodStart, prevStart, isMonthly, isPlatformAdmin, cancellationToken);
        summary["users"] = userResult.Summary;
        if (userResult.Chart is not null)
            charts["users"] = userResult.Chart;

        // Login activity
        var loginResult = await ComputeLoginActivityMetrics(periodStart, prevStart, isMonthly, isPlatformAdmin, cancellationToken);
        summary["loginActivity"] = loginResult.Summary;
        if (loginResult.Chart is not null)
            charts["loginActivity"] = loginResult.Chart;

        // Activity breakdown (audit logs)
        var activityResult = await ComputeActivityBreakdown(periodStart, isPlatformAdmin, cancellationToken);
        if (activityResult.ActionChart is not null)
            charts["activityByAction"] = activityResult.ActionChart;
        if (activityResult.EntityChart is not null)
            charts["activityByEntity"] = activityResult.EntityChart;

        // Storage
        if (sections.Contains("storage"))
        {
            var storageResult = await ComputeStorageMetrics(periodStart, prevStart, isMonthly, isPlatformAdmin, cancellationToken);
            summary["storage"] = storageResult.Summary;
            if (storageResult.Chart is not null)
                charts["storage"] = storageResult.Chart;
        }

        // API Keys
        if (sections.Contains("apiKeys"))
        {
            var apiKeyResult = await ComputeApiKeyMetrics(periodStart, prevStart, isPlatformAdmin, cancellationToken);
            summary["apiKeys"] = apiKeyResult;
        }

        // Reports
        if (sections.Contains("reports"))
        {
            var reportsResult = await ComputeReportMetrics(periodStart, prevStart, isPlatformAdmin, cancellationToken);
            summary["reports"] = reportsResult;
        }

        // Webhooks
        if (sections.Contains("webhooks"))
        {
            var webhooksResult = await ComputeWebhookMetrics(periodStart, prevStart, isMonthly, isPlatformAdmin, cancellationToken);
            summary["webhooks"] = webhooksResult.Summary;
            if (webhooksResult.Chart is not null)
                charts["webhookDeliveries"] = webhooksResult.Chart;
        }

        // Imports
        if (sections.Contains("imports"))
        {
            var importsResult = await ComputeImportMetrics(periodStart, prevStart, isPlatformAdmin, cancellationToken);
            summary["imports"] = importsResult;
        }

        // Revenue (platform admin only)
        if (sections.Contains("revenue"))
        {
            var revenueResult = await ComputeRevenueMetrics(periodStart, prevStart, isMonthly, cancellationToken);
            summary["revenue"] = revenueResult.Summary;
            if (revenueResult.Chart is not null)
                charts["revenue"] = revenueResult.Chart;
        }

        // Tenants (platform admin only)
        if (sections.Contains("tenants"))
        {
            var tenantsResult = await ComputeTenantMetrics(periodStart, prevStart, isMonthly, cancellationToken);
            summary["tenants"] = tenantsResult.Summary;
            if (tenantsResult.Chart is not null)
                charts["tenants"] = tenantsResult.Chart;
        }

        var dto = new DashboardAnalyticsDto(
            Period: request.Period,
            EnabledSections: sections.ToArray(),
            Summary: summary,
            Charts: charts);

        await cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(15), cancellationToken);

        logger.LogInformation(
            "Dashboard analytics computed for {Scope} with period {Period}",
            isPlatformAdmin ? "platform" : currentUser.TenantId.ToString(),
            request.Period);

        return Result.Success(dto);
    }

    // ─── Period Parsing ──────────────────────────────────────────────────────

    private static (DateTime PeriodStart, DateTime PrevStart) ParsePeriod(string period)
    {
        var now = DateTime.UtcNow;
        var duration = period switch
        {
            "7d" => TimeSpan.FromDays(7),
            "30d" => TimeSpan.FromDays(30),
            "90d" => TimeSpan.FromDays(90),
            "12m" => TimeSpan.FromDays(365),
            _ => TimeSpan.FromDays(30)
        };
        var periodStart = now - duration;
        var prevStart = periodStart - duration;
        return (periodStart, prevStart);
    }

    // ─── Trend Calculation ───────────────────────────────────────────────────

    private static decimal? CalcTrend(decimal current, decimal previous) =>
        previous == 0
            ? (current > 0 ? 100m : null)
            : Math.Round((current - previous) / previous * 100, 1);

    // ─── Users ───────────────────────────────────────────────────────────────

    private async Task<(SummaryMetric Summary, List<TimeSeriesPoint>? Chart)> ComputeUserMetrics(
        DateTime periodStart, DateTime prevStart, bool isMonthly, bool isPlatformAdmin,
        CancellationToken ct)
    {
        var baseQuery = isPlatformAdmin
            ? context.Users.IgnoreQueryFilters().AsNoTracking()
            : context.Users.AsNoTracking();

        var current = await baseQuery
            .Where(u => u.CreatedAt >= periodStart)
            .CountAsync(ct);

        var previous = await baseQuery
            .Where(u => u.CreatedAt >= prevStart && u.CreatedAt < periodStart)
            .CountAsync(ct);

        var trend = CalcTrend(current, previous);

        List<TimeSeriesPoint>? chart;
        if (isMonthly)
        {
            var groups = await baseQuery
                .Where(u => u.CreatedAt >= periodStart)
                .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync(ct);

            chart = groups.Select(g =>
                new TimeSeriesPoint($"{g.Year}-{g.Month:D2}", g.Count)).ToList();
        }
        else
        {
            var groups = await baseQuery
                .Where(u => u.CreatedAt >= periodStart)
                .GroupBy(u => u.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync(ct);

            chart = groups.Select(g =>
                new TimeSeriesPoint(g.Date.ToString("yyyy-MM-dd"), g.Count)).ToList();
        }

        return (new SummaryMetric(current, previous, trend), chart);
    }

    // ─── Login Activity ───────────────────────────────────────────────────────

    private async Task<(SummaryMetric Summary, List<TimeSeriesPoint>? Chart)> ComputeLoginActivityMetrics(
        DateTime periodStart, DateTime prevStart, bool isMonthly, bool isPlatformAdmin,
        CancellationToken ct)
    {
        // LoginHistory has no TenantId — filter by user scope when tenant admin
        var baseQuery = context.LoginHistory.AsNoTracking();

        IQueryable<Domain.Identity.Entities.LoginHistory> scopedQuery;
        if (isPlatformAdmin)
        {
            scopedQuery = baseQuery;
        }
        else
        {
            var tenantUserEmails = context.Users
                .AsNoTracking()
                .Where(u => u.TenantId == currentUser.TenantId)
                .Select(u => u.Email.Value);

            scopedQuery = baseQuery.Where(l => tenantUserEmails.Contains(l.Email));
        }

        var current = await scopedQuery
            .Where(l => l.CreatedAt >= periodStart)
            .CountAsync(ct);

        var previous = await scopedQuery
            .Where(l => l.CreatedAt >= prevStart && l.CreatedAt < periodStart)
            .CountAsync(ct);

        var trend = CalcTrend(current, previous);

        List<TimeSeriesPoint>? chart;
        if (isMonthly)
        {
            var groups = await scopedQuery
                .Where(l => l.CreatedAt >= periodStart)
                .GroupBy(l => new { l.CreatedAt.Year, l.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync(ct);

            chart = groups.Select(g =>
                new TimeSeriesPoint($"{g.Year}-{g.Month:D2}", g.Count)).ToList();
        }
        else
        {
            var groups = await scopedQuery
                .Where(l => l.CreatedAt >= periodStart)
                .GroupBy(l => l.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync(ct);

            chart = groups.Select(g =>
                new TimeSeriesPoint(g.Date.ToString("yyyy-MM-dd"), g.Count)).ToList();
        }

        return (new SummaryMetric(current, previous, trend), chart);
    }

    // ─── Activity Breakdown ───────────────────────────────────────────────────

    private async Task<(List<TimeSeriesPoint>? ActionChart, List<TimeSeriesPoint>? EntityChart)>
        ComputeActivityBreakdown(DateTime periodStart, bool isPlatformAdmin, CancellationToken ct)
    {
        var baseQuery = isPlatformAdmin
            ? context.AuditLogs.IgnoreQueryFilters().AsNoTracking()
            : context.AuditLogs.AsNoTracking();

        var inPeriod = baseQuery.Where(a => a.PerformedAt >= periodStart);

        var actionGroups = await inPeriod
            .GroupBy(a => a.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(ct);

        var actionChart = actionGroups.Select(g =>
            new TimeSeriesPoint(g.Action.ToString(), g.Count)).ToList();

        var entityGroups = await inPeriod
            .GroupBy(a => a.EntityType)
            .Select(g => new { EntityType = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync(ct);

        var entityChart = entityGroups.Select(g =>
            new TimeSeriesPoint(g.EntityType.ToString(), g.Count)).ToList();

        return (actionChart, entityChart);
    }

    // ─── Storage ─────────────────────────────────────────────────────────────

    private async Task<(SummaryMetric Summary, List<TimeSeriesPoint>? Chart)> ComputeStorageMetrics(
        DateTime periodStart, DateTime prevStart, bool isMonthly, bool isPlatformAdmin,
        CancellationToken ct)
    {
        var baseQuery = isPlatformAdmin
            ? context.FileMetadata.IgnoreQueryFilters().AsNoTracking()
            : context.FileMetadata.AsNoTracking();

        var current = await baseQuery
            .Where(f => f.CreatedAt >= periodStart)
            .SumAsync(f => (decimal)f.Size, ct);

        var previous = await baseQuery
            .Where(f => f.CreatedAt >= prevStart && f.CreatedAt < periodStart)
            .SumAsync(f => (decimal)f.Size, ct);

        var trend = CalcTrend(current, previous);

        // Cumulative chart
        List<TimeSeriesPoint>? chart;
        if (isMonthly)
        {
            var groups = await baseQuery
                .Where(f => f.CreatedAt >= periodStart)
                .GroupBy(f => new { f.CreatedAt.Year, f.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(f => (decimal)f.Size) })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync(ct);

            chart = groups.Select(g =>
                new TimeSeriesPoint($"{g.Year}-{g.Month:D2}", g.Total)).ToList();
        }
        else
        {
            var groups = await baseQuery
                .Where(f => f.CreatedAt >= periodStart)
                .GroupBy(f => f.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(f => (decimal)f.Size) })
                .OrderBy(g => g.Date)
                .ToListAsync(ct);

            chart = groups.Select(g =>
                new TimeSeriesPoint(g.Date.ToString("yyyy-MM-dd"), g.Total)).ToList();
        }

        return (new SummaryMetric(current, previous, trend), chart);
    }

    // ─── API Keys ────────────────────────────────────────────────────────────

    private async Task<SummaryMetric> ComputeApiKeyMetrics(
        DateTime periodStart, DateTime prevStart, bool isPlatformAdmin, CancellationToken ct)
    {
        var baseQuery = isPlatformAdmin
            ? context.ApiKeys.IgnoreQueryFilters().AsNoTracking()
            : context.ApiKeys.AsNoTracking();

        var current = await baseQuery
            .Where(k => !k.IsRevoked && k.CreatedAt >= periodStart)
            .CountAsync(ct);

        var previous = await baseQuery
            .Where(k => !k.IsRevoked && k.CreatedAt >= prevStart && k.CreatedAt < periodStart)
            .CountAsync(ct);

        return new SummaryMetric(current, previous, CalcTrend(current, previous));
    }

    // ─── Reports ─────────────────────────────────────────────────────────────

    private async Task<SummaryMetric> ComputeReportMetrics(
        DateTime periodStart, DateTime prevStart, bool isPlatformAdmin, CancellationToken ct)
    {
        var baseQuery = isPlatformAdmin
            ? context.ReportRequests.IgnoreQueryFilters().AsNoTracking()
            : context.ReportRequests.AsNoTracking();

        var current = await baseQuery
            .Where(r => r.CreatedAt >= periodStart)
            .CountAsync(ct);

        var previous = await baseQuery
            .Where(r => r.CreatedAt >= prevStart && r.CreatedAt < periodStart)
            .CountAsync(ct);

        return new SummaryMetric(current, previous, CalcTrend(current, previous));
    }

    // ─── Webhooks ────────────────────────────────────────────────────────────

    private async Task<(SummaryMetric Summary, List<TimeSeriesPoint>? Chart)> ComputeWebhookMetrics(
        DateTime periodStart, DateTime prevStart, bool isMonthly, bool isPlatformAdmin,
        CancellationToken ct)
    {
        var endpointQuery = isPlatformAdmin
            ? context.WebhookEndpoints.IgnoreQueryFilters().AsNoTracking()
            : context.WebhookEndpoints.AsNoTracking();

        var deliveryQuery = isPlatformAdmin
            ? context.WebhookDeliveries.IgnoreQueryFilters().AsNoTracking()
            : context.WebhookDeliveries.AsNoTracking();

        var activeEndpoints = await endpointQuery.CountAsync(e => e.IsActive, ct);

        var current = await deliveryQuery
            .Where(d => d.CreatedAt >= periodStart)
            .CountAsync(ct);

        var previous = await deliveryQuery
            .Where(d => d.CreatedAt >= prevStart && d.CreatedAt < periodStart)
            .CountAsync(ct);

        var trend = CalcTrend(current, previous);

        List<TimeSeriesPoint>? chart;
        if (isMonthly)
        {
            var groups = await deliveryQuery
                .Where(d => d.CreatedAt >= periodStart)
                .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync(ct);

            chart = groups.Select(g =>
                new TimeSeriesPoint($"{g.Year}-{g.Month:D2}", g.Count)).ToList();
        }
        else
        {
            var groups = await deliveryQuery
                .Where(d => d.CreatedAt >= periodStart)
                .GroupBy(d => d.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync(ct);

            chart = groups.Select(g =>
                new TimeSeriesPoint(g.Date.ToString("yyyy-MM-dd"), g.Count)).ToList();
        }

        return (new SummaryMetric(activeEndpoints, 0, null), chart);
    }

    // ─── Imports ────────────────────────────────────────────────────────────

    private async Task<SummaryMetric> ComputeImportMetrics(
        DateTime periodStart, DateTime prevStart, bool isPlatformAdmin, CancellationToken ct)
    {
        var baseQuery = isPlatformAdmin
            ? context.ImportJobs.IgnoreQueryFilters().AsNoTracking()
            : context.ImportJobs.AsNoTracking();

        var current = await baseQuery
            .Where(j => j.CreatedAt >= periodStart)
            .CountAsync(ct);

        var previous = await baseQuery
            .Where(j => j.CreatedAt >= prevStart && j.CreatedAt < periodStart)
            .CountAsync(ct);

        return new SummaryMetric(current, previous, CalcTrend(current, previous));
    }

    // ─── Revenue (platform admin only) ───────────────────────────────────────

    private async Task<(SummaryMetric Summary, List<TimeSeriesPoint>? Chart)> ComputeRevenueMetrics(
        DateTime periodStart, DateTime prevStart, bool isMonthly, CancellationToken ct)
    {
        var baseQuery = context.PaymentRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed);

        var current = await baseQuery
            .Where(p => p.CreatedAt >= periodStart)
            .SumAsync(p => p.Amount, ct);

        var previous = await baseQuery
            .Where(p => p.CreatedAt >= prevStart && p.CreatedAt < periodStart)
            .SumAsync(p => p.Amount, ct);

        var trend = CalcTrend(current, previous);

        List<TimeSeriesPoint>? chart;
        if (isMonthly)
        {
            var groups = await baseQuery
                .Where(p => p.CreatedAt >= periodStart)
                .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync(ct);

            chart = groups.Select(g =>
                new TimeSeriesPoint($"{g.Year}-{g.Month:D2}", g.Total)).ToList();
        }
        else
        {
            var groups = await baseQuery
                .Where(p => p.CreatedAt >= periodStart)
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(p => p.Amount) })
                .OrderBy(g => g.Date)
                .ToListAsync(ct);

            chart = groups.Select(g =>
                new TimeSeriesPoint(g.Date.ToString("yyyy-MM-dd"), g.Total)).ToList();
        }

        return (new SummaryMetric(current, previous, trend), chart);
    }

    // ─── Tenants (platform admin only) ───────────────────────────────────────

    private async Task<(SummaryMetric Summary, List<TimeSeriesPoint>? Chart)> ComputeTenantMetrics(
        DateTime periodStart, DateTime prevStart, bool isMonthly, CancellationToken ct)
    {
        var baseQuery = context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking();

        var current = await baseQuery
            .Where(t => t.CreatedAt >= periodStart)
            .CountAsync(ct);

        var previous = await baseQuery
            .Where(t => t.CreatedAt >= prevStart && t.CreatedAt < periodStart)
            .CountAsync(ct);

        var trend = CalcTrend(current, previous);

        List<TimeSeriesPoint>? chart;
        if (isMonthly)
        {
            var groups = await baseQuery
                .Where(t => t.CreatedAt >= periodStart)
                .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync(ct);

            chart = groups.Select(g =>
                new TimeSeriesPoint($"{g.Year}-{g.Month:D2}", g.Count)).ToList();
        }
        else
        {
            var groups = await baseQuery
                .Where(t => t.CreatedAt >= periodStart)
                .GroupBy(t => t.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync(ct);

            chart = groups.Select(g =>
                new TimeSeriesPoint(g.Date.ToString("yyyy-MM-dd"), g.Count)).ToList();
        }

        return (new SummaryMetric(current, previous, trend), chart);
    }
}
