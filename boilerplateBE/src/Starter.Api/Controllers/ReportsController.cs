using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Reports.Commands.DeleteReport;
using Starter.Application.Features.Reports.Commands.RequestReport;
using Starter.Application.Features.Reports.Queries.GetReportDownload;
using Starter.Application.Features.Reports.Queries.GetReports;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// Report generation and download endpoints.
/// </summary>
public sealed class ReportsController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Request an async report generation.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.System.ExportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RequestReport([FromBody] RequestReportRequest request)
    {
        var command = new RequestReportCommand(
            request.ReportType,
            request.Format,
            request.Filters,
            request.ForceRefresh);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Get paginated list of report requests.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.System.ExportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReports([FromQuery] GetReportsQuery query)
    {
        var result = await Mediator.Send(query);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get a signed download URL for a completed report.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [Authorize(Policy = Permissions.System.ExportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDownloadUrl(Guid id)
    {
        var result = await Mediator.Send(new GetReportDownloadQuery(id));
        return HandleResult(result);
    }

    /// <summary>
    /// Delete a report request.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.System.ExportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteReport(Guid id)
    {
        var result = await Mediator.Send(new DeleteReportCommand(id));
        return HandleResult(result);
    }
}

/// <summary>
/// Request body for requesting a new report.
/// </summary>
public sealed record RequestReportRequest(
    string ReportType,
    string Format,
    string? Filters,
    bool ForceRefresh = false);
