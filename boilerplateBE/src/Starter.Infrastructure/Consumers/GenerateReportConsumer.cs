using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Constants;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Messages;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Infrastructure.Persistence;

namespace Starter.Infrastructure.Consumers;

public sealed class GenerateReportConsumer(
    ApplicationDbContext dbContext,
    IExportService exportService,
    IFileService fileService,
    INotificationService notificationService,
    ISettingsService settingsService,
    ILogger<GenerateReportConsumer> logger) : IConsumer<GenerateReportMessage>
{
    public async Task Consume(ConsumeContext<GenerateReportMessage> context)
    {
        var reportRequestId = context.Message.ReportRequestId;
        var cancellationToken = context.CancellationToken;

        // Use IgnoreQueryFilters — consumer runs without HTTP context, no tenant scoping
        var report = await dbContext.ReportRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == reportRequestId, cancellationToken);

        if (report is null)
        {
            logger.LogWarning("Report request {ReportRequestId} not found", reportRequestId);
            return;
        }

        try
        {
            report.MarkProcessing();
            await dbContext.SaveChangesAsync(cancellationToken);

            var fileBytes = await GenerateReportDataAsync(report, cancellationToken);

            if (fileBytes is null || fileBytes.Length == 0)
                throw new InvalidOperationException($"Report generation for {report.ReportType.Name} produced no data.");

            var contentType = report.Format == ReportFormat.Csv ? "text/csv" : "application/pdf";
            var fileName = report.GetFileName();

            // Determine expiration from system settings for time-sensitive reports
            DateTime? expiresAt = null;
            if (HasTimeSensitiveFilters(report.Filters))
            {
                var hoursStr = await settingsService.GetValueAsync(FileSettings.ReportExpirationHoursKey, report.TenantId, cancellationToken);
                var hours = int.TryParse(hoursStr, out var h) ? h : FileSettings.ReportExpirationHoursDefault;
                expiresAt = DateTime.UtcNow.AddHours(hours);
            }

            using var stream = new MemoryStream(fileBytes);
            var fileMetadata = await fileService.CreateSystemFileAsync(
                stream, fileName, contentType, fileBytes.Length,
                FileCategory.Report, report.TenantId,
                $"Generated report: {report.ReportType.Name}",
                expiresAt, cancellationToken);

            report.MarkCompleted(fileMetadata.Id, fileName, expiresAt);
            await dbContext.SaveChangesAsync(cancellationToken);

            await notificationService.CreateAsync(
                report.RequestedBy,
                report.TenantId,
                "report_completed",
                "Report Ready",
                $"Your {report.ReportType.Name} report is ready for download.",
                JsonSerializer.Serialize(new { reportId = report.Id }),
                cancellationToken);

            logger.LogInformation(
                "Report {ReportRequestId} completed successfully. File: {FileName}",
                reportRequestId, fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate report {ReportRequestId}", reportRequestId);

            try
            {
                report.MarkFailed(ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message);
                await dbContext.SaveChangesAsync(cancellationToken);

                await notificationService.CreateAsync(
                    report.RequestedBy,
                    report.TenantId,
                    "report_failed",
                    "Report Failed",
                    $"Your {report.ReportType.Name} report failed to generate. Please try again.",
                    JsonSerializer.Serialize(new { reportId = report.Id, error = ex.Message }),
                    cancellationToken);
            }
            catch (Exception innerEx)
            {
                logger.LogError(innerEx, "Failed to mark report {ReportRequestId} as failed", reportRequestId);
            }
        }
    }

    private async Task<byte[]> GenerateReportDataAsync(ReportRequest report, CancellationToken cancellationToken)
    {
        if (report.ReportType == ReportType.AuditLogs)
            return await GenerateAuditLogsReportAsync(report, cancellationToken);

        if (report.ReportType == ReportType.Users)
            return await GenerateUsersReportAsync(report, cancellationToken);

        if (report.ReportType == ReportType.Files)
            return await GenerateFilesReportAsync(report, cancellationToken);

        throw new InvalidOperationException($"Unknown report type: {report.ReportType.Name}");
    }

    private async Task<byte[]> GenerateAuditLogsReportAsync(ReportRequest report, CancellationToken cancellationToken)
    {
        var query = dbContext.AuditLogs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => report.TenantId == null || a.TenantId == report.TenantId)
            .AsQueryable();

        var filters = ParseFilters(report.Filters);

        if (filters.TryGetValue("dateFrom", out var dateFromStr) && DateTime.TryParse(dateFromStr, out var dateFrom))
            query = query.Where(a => a.PerformedAt >= dateFrom);

        if (filters.TryGetValue("dateTo", out var dateToStr) && DateTime.TryParse(dateToStr, out var dateTo))
            query = query.Where(a => a.PerformedAt <= dateTo);

        if (filters.TryGetValue("action", out var actionStr) && Enum.TryParse<AuditAction>(actionStr, true, out var action))
            query = query.Where(a => a.Action == action);

        if (filters.TryGetValue("searchTerm", out var searchTerm) && !string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(a =>
                (a.PerformedByName != null && a.PerformedByName.ToLower().Contains(term)) ||
                (a.CorrelationId != null && a.CorrelationId.ToLower().Contains(term)));
        }

        query = query.OrderByDescending(a => a.PerformedAt);

        var data = await query.ToListAsync(cancellationToken);

        var columnHeaders = new[] { "Entity Type", "Entity ID", "Action", "Performed By", "Performed At", "IP Address", "Correlation ID" };
        Func<AuditLog, object[]> rowMapper = a => new object[]
        {
            a.EntityType.ToString(),
            a.EntityId.ToString(),
            a.Action.ToString(),
            a.PerformedByName ?? a.PerformedBy?.ToString() ?? "",
            a.PerformedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            a.IpAddress ?? "",
            a.CorrelationId ?? ""
        };

        return report.Format == ReportFormat.Csv
            ? exportService.GenerateCsv(data, columnHeaders, rowMapper)
            : exportService.GeneratePdf(data, "Audit Logs Report", columnHeaders, rowMapper);
    }

    private async Task<byte[]> GenerateUsersReportAsync(ReportRequest report, CancellationToken cancellationToken)
    {
        var query = dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => report.TenantId == null || u.TenantId == report.TenantId)
            .AsQueryable();

        var filters = ParseFilters(report.Filters);

        if (filters.TryGetValue("status", out var statusStr))
        {
            var status = Domain.Identity.Enums.UserStatus.FromName(statusStr);
            if (status is not null)
                query = query.Where(u => u.Status == status);
        }

        if (filters.TryGetValue("searchTerm", out var searchTerm) && !string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(term) ||
                u.Email.Value.ToLower().Contains(term) ||
                u.FullName.FirstName.ToLower().Contains(term) ||
                u.FullName.LastName.ToLower().Contains(term));
        }

        query = query.OrderBy(u => u.CreatedAt);

        var data = await query.ToListAsync(cancellationToken);

        var columnHeaders = new[] { "Username", "Email", "First Name", "Last Name", "Status", "Email Confirmed", "Created At" };
        Func<Domain.Identity.Entities.User, object[]> rowMapper = u => new object[]
        {
            u.Username,
            u.Email.Value,
            u.FullName.FirstName,
            u.FullName.LastName,
            u.Status.Name,
            u.EmailConfirmed ? "Yes" : "No",
            u.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        };

        return report.Format == ReportFormat.Csv
            ? exportService.GenerateCsv(data, columnHeaders, rowMapper)
            : exportService.GeneratePdf(data, "Users Report", columnHeaders, rowMapper);
    }

    private async Task<byte[]> GenerateFilesReportAsync(ReportRequest report, CancellationToken cancellationToken)
    {
        var query = dbContext.FileMetadata
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(f => report.TenantId == null || f.TenantId == report.TenantId)
            .AsQueryable();

        var filters = ParseFilters(report.Filters);

        if (filters.TryGetValue("category", out var categoryStr) && Enum.TryParse<FileCategory>(categoryStr, true, out var category))
            query = query.Where(f => f.Category == category);

        if (filters.TryGetValue("entityType", out var entityType) && !string.IsNullOrWhiteSpace(entityType))
            query = query.Where(f => f.EntityType == entityType);

        if (filters.TryGetValue("searchTerm", out var searchTerm) && !string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(f =>
                f.FileName.ToLower().Contains(term) ||
                (f.Description != null && f.Description.ToLower().Contains(term)));
        }

        query = query.OrderByDescending(f => f.CreatedAt);

        var data = await query.ToListAsync(cancellationToken);

        var columnHeaders = new[] { "File Name", "Content Type", "Size (bytes)", "Category", "Uploaded By", "Public", "Created At" };
        Func<FileMetadata, object[]> rowMapper = f => new object[]
        {
            f.FileName,
            f.ContentType,
            f.Size.ToString(),
            f.Category.ToString(),
            f.UploadedBy.ToString(),
            f.IsPublic ? "Yes" : "No",
            f.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        };

        return report.Format == ReportFormat.Csv
            ? exportService.GenerateCsv(data, columnHeaders, rowMapper)
            : exportService.GeneratePdf(data, "Files Report", columnHeaders, rowMapper);
    }

    private static Dictionary<string, string> ParseFilters(string? filtersJson)
    {
        if (string.IsNullOrWhiteSpace(filtersJson))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(filtersJson);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => property.Value.GetRawText()
                };

                if (value is not null)
                    result[property.Name] = value;
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool HasTimeSensitiveFilters(string? filtersJson)
    {
        if (string.IsNullOrWhiteSpace(filtersJson))
            return true; // No date filter means current data — time-sensitive

        var filters = ParseFilters(filtersJson);

        // If both dateFrom and dateTo are specified, it's a fixed range — not time-sensitive
        return !(filters.ContainsKey("dateFrom") && filters.ContainsKey("dateTo"));
    }
}
