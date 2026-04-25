using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Messages;
using Starter.Application.Features.Reports.DTOs;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Constants;
using Starter.Shared.Results;

namespace Starter.Application.Features.Reports.Commands.RequestReport;

internal sealed class RequestReportCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IMessagePublisher messagePublisher,
    ISettingsProvider settingsProvider) : IRequestHandler<RequestReportCommand, Result<ReportDto>>
{
    public async Task<Result<ReportDto>> Handle(RequestReportCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
            return Result.Failure<ReportDto>(UserErrors.Unauthorized());

        var reportType = ReportType.FromName(request.ReportType);
        if (reportType is null)
            return Result.Failure<ReportDto>(Error.Validation("Report.InvalidType", "Invalid report type."));

        var format = ReportFormat.FromName(request.Format);
        if (format is null)
            return Result.Failure<ReportDto>(Error.Validation("Report.InvalidFormat", "Invalid report format."));

        var cacheDurationMinutes = await settingsProvider.GetIntAsync("Reports.CacheDurationMinutes", 60, cancellationToken);
        var filterHash = ComputeFilterHash(reportType, format, request.Filters, cacheDurationMinutes);

        var forceRefresh = request.ForceRefresh && currentUserService.HasPermission(Starter.Shared.Constants.Permissions.System.ForceExport);

        if (!forceRefresh)
        {
            var existing = await context.Set<ReportRequest>()
                .AsNoTracking()
                .Where(r => r.FilterHash == filterHash
                    && r.TenantId == currentUserService.TenantId
                    && r.Status == ReportStatus.Completed)
                .OrderByDescending(r => r.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null && (existing.ExpiresAt is null || existing.ExpiresAt > DateTime.UtcNow))
            {
                return Result.Success(existing.ToDto());
            }
        }

        var reportRequest = ReportRequest.Create(
            userId.Value,
            currentUserService.TenantId,
            reportType,
            format,
            request.Filters,
            filterHash);

        context.Set<ReportRequest>().Add(reportRequest);

        // Publish before SaveChanges so the message is scheduled on the
        // collector and flushed atomically with the report_requests row by the
        // IntegrationEventOutboxInterceptor. The previous "Save → Publish"
        // order dropped the message because the publisher schedules but the
        // collector is drained only during SaveChanges.
        await messagePublisher.PublishAsync(new GenerateReportMessage(reportRequest.Id), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(reportRequest.ToDto());
    }

    private static string ComputeFilterHash(ReportType reportType, ReportFormat format, string? filters, int cacheDurationMinutes)
    {
        var normalizedFilters = NormalizeJson(filters);

        // Date bucket: truncate to cache duration window for deterministic hashing
        var totalMinutes = (long)Math.Floor((DateTime.UtcNow - DateTime.UnixEpoch).TotalMinutes);
        var bucket = cacheDurationMinutes > 0 ? totalMinutes / cacheDurationMinutes : totalMinutes;

        var input = $"{reportType.Name}|{format.Name}|{normalizedFilters}|{bucket}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string NormalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var sortedProperties = doc.RootElement
                .EnumerateObject()
                .OrderBy(p => p.Name, StringComparer.Ordinal);

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
            writer.WriteStartObject();
            foreach (var property in sortedProperties)
            {
                property.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return json.Trim();
        }
    }
}
