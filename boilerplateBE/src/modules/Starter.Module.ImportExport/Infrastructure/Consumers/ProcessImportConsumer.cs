using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Enums;
using Starter.Domain.Common;
using Starter.Module.ImportExport.Application.Abstractions;
using Starter.Module.ImportExport.Application.Messages;
using Starter.Module.ImportExport.Domain.Entities;
using Starter.Module.ImportExport.Domain.Enums;
using Starter.Module.ImportExport.Infrastructure.Persistence;

namespace Starter.Module.ImportExport.Infrastructure.Consumers;

public sealed class ProcessImportConsumer(IServiceScopeFactory scopeFactory) : IConsumer<ProcessImportMessage>
{
    public async Task Consume(ConsumeContext<ProcessImportMessage> context)
    {
        var ct = context.CancellationToken;

        using var scope = scopeFactory.CreateScope();
        var importExportContext = scope.ServiceProvider.GetRequiredService<ImportExportDbContext>();
        var appContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var registry = scope.ServiceProvider.GetRequiredService<IImportExportRegistry>();
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ProcessImportConsumer>>();

        var job = await importExportContext.ImportJobs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(j => j.Id == context.Message.ImportJobId, ct);

        if (job is null)
        {
            logger.LogError("Import job {ImportJobId} not found", context.Message.ImportJobId);
            return;
        }

        try
        {
            // ── Validation phase ──────────────────────────────────────────
            job.MarkValidating();
            await importExportContext.SaveChangesAsync(ct);

            var definition = registry.GetDefinition(job.EntityType);
            if (definition is null)
            {
                job.MarkFailed($"Unknown entity type: {job.EntityType}");
                await importExportContext.SaveChangesAsync(ct);
                await SendNotificationAsync(notificationService, logger, job, "import_failed",
                    "Import Failed", $"Import failed: unknown entity type '{job.EntityType}'.", ct);
                return;
            }

            var fileMetadata = await appContext.Set<FileMetadata>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == job.FileId, ct);

            if (fileMetadata is null)
            {
                job.MarkFailed("Source file not found.");
                await importExportContext.SaveChangesAsync(ct);
                await SendNotificationAsync(notificationService, logger, job, "import_failed",
                    "Import Failed", "Import failed: source file could not be found.", ct);
                return;
            }

            Stream validationStream;
            try
            {
                validationStream = await storageService.DownloadAsync(fileMetadata.StorageKey, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to download import file {StorageKey}", fileMetadata.StorageKey);
                job.MarkFailed("Failed to download source file.");
                await importExportContext.SaveChangesAsync(ct);
                await SendNotificationAsync(notificationService, logger, job, "import_failed",
                    "Import Failed", "Import failed: could not download the source file.", ct);
                return;
            }

            string[] headers;
            int totalRows = 0;

            using (validationStream)
            using (var reader = new StreamReader(validationStream, Encoding.UTF8))
            {
                var headerLine = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    job.MarkFailed("CSV file is empty or has no header row.");
                    await importExportContext.SaveChangesAsync(ct);
                    await SendNotificationAsync(notificationService, logger, job, "import_failed",
                        "Import Failed", "Import failed: CSV file is empty or has no header row.", ct);
                    return;
                }

                headers = ParseCsvLine(headerLine);

                // Validate required import fields exist
                var requiredFields = definition.Fields
                    .Where(f => f.Required && !f.ExportOnly)
                    .Select(f => f.DisplayName)
                    .ToArray();

                var missingColumns = requiredFields
                    .Where(r => !headers.Any(h => string.Equals(h, r, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();

                if (missingColumns.Length > 0)
                {
                    var errorMsg = $"Header mismatch: missing required columns: {string.Join(", ", missingColumns)}";
                    job.MarkFailed(errorMsg);
                    await importExportContext.SaveChangesAsync(ct);
                    await SendNotificationAsync(notificationService, logger, job, "import_failed",
                        "Import Failed", $"Import failed: {errorMsg}", ct);
                    return;
                }

                // Count total rows
                string? line;
                while ((line = await reader.ReadLineAsync(ct)) is not null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        totalRows++;
                }
            }

            job.SetTotalRows(totalRows);
            await importExportContext.SaveChangesAsync(ct);

            // ── Processing phase ──────────────────────────────────────────
            job.MarkProcessing();
            await importExportContext.SaveChangesAsync(ct);

            Stream processingStream;
            try
            {
                processingStream = await storageService.DownloadAsync(fileMetadata.StorageKey, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to re-download import file {StorageKey}", fileMetadata.StorageKey);
                job.MarkFailed("Failed to download source file for processing.");
                await importExportContext.SaveChangesAsync(ct);
                await SendNotificationAsync(notificationService, logger, job, "import_failed",
                    "Import Failed", "Import failed: could not download the source file for processing.", ct);
                return;
            }

            if (definition.ImportRowProcessorType is null)
            {
                job.MarkFailed($"No import processor registered for entity type: {job.EntityType}");
                await importExportContext.SaveChangesAsync(ct);
                await SendNotificationAsync(notificationService, logger, job, "import_failed",
                    "Import Failed", $"Import failed: no processor found for '{job.EntityType}'.", ct);
                return;
            }

            var processor = (IImportRowProcessor)scope.ServiceProvider.GetRequiredService(definition.ImportRowProcessorType);

            var errorRows = new List<(int RowNum, Dictionary<string, string> Data, string Status, string Error)>();
            int created = 0, updated = 0, skipped = 0, failed = 0, rowNum = 0;

            using (processingStream)
            using (var reader = new StreamReader(processingStream, Encoding.UTF8))
            {
                var headerLine = await reader.ReadLineAsync(ct);
                var fileHeaders = headerLine is not null ? ParseCsvLine(headerLine) : headers;

                while (await reader.ReadLineAsync(ct) is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    rowNum++;
                    var fields = ParseCsvLine(line);
                    var row = MapToDict(fileHeaders, fields, definition);

                    try
                    {
                        var result = await processor.ProcessRowAsync(row, job.ConflictMode, job.TenantId, ct);
                        switch (result.Status)
                        {
                            case ImportRowStatus.Created:
                                created++;
                                break;
                            case ImportRowStatus.Updated:
                                updated++;
                                break;
                            case ImportRowStatus.Skipped:
                                skipped++;
                                errorRows.Add((rowNum, row, "Skipped", result.ErrorMessage ?? "Duplicate"));
                                break;
                            case ImportRowStatus.Failed:
                                failed++;
                                errorRows.Add((rowNum, row, "Failed", result.ErrorMessage ?? "Unknown error"));
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        errorRows.Add((rowNum, row, "Failed", ex.Message));
                    }

                    // Batch commit every 50 rows
                    if (rowNum % 50 == 0)
                    {
                        await importExportContext.SaveChangesAsync(ct);
                        job.UpdateProgress(rowNum, created, updated, skipped, failed);
                        await importExportContext.SaveChangesAsync(ct);
                    }
                }
            }

            // Final batch save
            await importExportContext.SaveChangesAsync(ct);
            job.UpdateProgress(rowNum, created, updated, skipped, failed);
            await importExportContext.SaveChangesAsync(ct);

            // ── Error report generation ───────────────────────────────────
            Guid? resultsFileId = null;
            if (errorRows.Count > 0)
            {
                try
                {
                    var errorCsv = BuildErrorReportCsv(headers, errorRows);
                    var errorBytes = Encoding.UTF8.GetBytes(errorCsv);
                    var errorFileName = $"{job.EntityType}_import_errors_{job.Id:N}.csv";

                    using var errorStream = new MemoryStream(errorBytes);
                    var errorFile = await fileService.CreateSystemFileAsync(
                        errorStream, errorFileName, "text/csv", errorBytes.Length,
                        FileCategory.Export, job.TenantId,
                        $"Import error report for job {job.Id}",
                        null, ct);

                    resultsFileId = errorFile.Id;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to generate error report for import job {ImportJobId}", job.Id);
                }
            }

            // ── Completion ────────────────────────────────────────────────
            if (created == 0 && updated == 0)
            {
                job.MarkFailed("No rows imported successfully.");
                await importExportContext.SaveChangesAsync(ct);
                await SendNotificationAsync(notificationService, logger, job, "import_failed",
                    "Import Failed", $"Import completed but no rows were imported successfully. {failed} failed, {skipped} skipped.", ct);
            }
            else if (failed == 0 && skipped == 0)
            {
                job.MarkCompleted(resultsFileId);
                await importExportContext.SaveChangesAsync(ct);
                await SendNotificationAsync(notificationService, logger, job, "import_completed",
                    "Import Completed",
                    $"Your {job.EntityType} import completed successfully. {created} created, {updated} updated.", ct);
            }
            else
            {
                job.MarkPartialSuccess(resultsFileId ?? Guid.Empty);
                await importExportContext.SaveChangesAsync(ct);
                await SendNotificationAsync(notificationService, logger, job, "import_partial",
                    "Import Partially Completed",
                    $"Your {job.EntityType} import completed with some issues. {created} created, {updated} updated, {skipped} skipped, {failed} failed.", ct);
            }

            logger.LogInformation(
                "Import job {ImportJobId} finished. Created={Created}, Updated={Updated}, Skipped={Skipped}, Failed={Failed}",
                job.Id, created, updated, skipped, failed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error processing import job {ImportJobId}", job.Id);

            try
            {
                var msg = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                job.MarkFailed(msg);
                await importExportContext.SaveChangesAsync(ct);
                await SendNotificationAsync(notificationService, logger, job, "import_failed",
                    "Import Failed", "An unexpected error occurred during import. Please try again.", ct);
            }
            catch (Exception innerEx)
            {
                logger.LogError(innerEx, "Failed to mark import job {ImportJobId} as failed", job.Id);
            }
        }
    }

    private static async Task SendNotificationAsync(
        INotificationService notificationService,
        ILogger logger,
        ImportJob job,
        string type, string title, string message,
        CancellationToken ct)
    {
        try
        {
            await notificationService.CreateAsync(
                job.RequestedBy,
                job.TenantId,
                type,
                title,
                message,
                JsonSerializer.Serialize(new { importJobId = job.Id }),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send import notification for job {ImportJobId}", job.Id);
        }
    }

    private static string BuildErrorReportCsv(
        string[] originalHeaders,
        List<(int RowNum, Dictionary<string, string> Data, string Status, string Error)> errorRows)
    {
        var sb = new StringBuilder();

        // Header row: original columns + Row + Status + Error
        var allHeaders = originalHeaders.Concat(new[] { "Row", "Status", "Error" }).ToArray();
        sb.AppendLine(string.Join(",", allHeaders.Select(EscapeCsvField)));

        foreach (var (rowNum, data, status, error) in errorRows)
        {
            var cells = originalHeaders
                .Select(h => data.TryGetValue(h, out var v) ? EscapeCsvField(v) : "")
                .Concat(new[]
                {
                    EscapeCsvField(rowNum.ToString()),
                    EscapeCsvField(status),
                    EscapeCsvField(error)
                });

            sb.AppendLine(string.Join(",", cells));
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString().Trim());
        return fields.ToArray();
    }

    private static Dictionary<string, string> MapToDict(string[] headers, string[] fields, EntityImportExportDefinition? definition = null)
    {
        // Build DisplayName → Name lookup from definition
        var displayToName = definition?.Fields
            .ToDictionary(f => f.DisplayName, f => f.Name, StringComparer.OrdinalIgnoreCase);

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            // Map CSV header (DisplayName) to internal field Name
            var key = displayToName is not null && displayToName.TryGetValue(headers[i], out var internalName)
                ? internalName
                : headers[i];
            dict[key] = i < fields.Length ? fields[i] : string.Empty;
        }
        return dict;
    }
}
