using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.ImportExport.Commands.DeleteImportJob;
using Starter.Application.Features.ImportExport.Commands.StartImport;
using Starter.Application.Features.ImportExport.Queries.GetEntityTypes;
using Starter.Application.Features.ImportExport.Queries.GetImportErrorReport;
using Starter.Application.Features.ImportExport.Queries.GetImportJobById;
using Starter.Application.Features.ImportExport.Queries.GetImportJobs;
using Starter.Application.Features.ImportExport.Queries.GetImportTemplate;
using Starter.Application.Features.ImportExport.Queries.PreviewImport;
using Starter.Domain.ImportExport.Enums;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// Import and export endpoints for entity data management.
/// </summary>
public sealed class ImportExportController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Get all available entity types that support import or export.
    /// </summary>
    [HttpGet("types")]
    [Authorize(Policy = Permissions.System.ExportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEntityTypes(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetEntityTypesQuery(), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Download a CSV import template for the given entity type.
    /// </summary>
    [HttpGet("{entityType}/template")]
    [Authorize(Policy = Permissions.System.ImportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImportTemplate(string entityType, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetImportTemplateQuery(entityType), ct);
        if (result.IsFailure)
            return HandleResult(result);

        return File(result.Value, "text/csv", $"{entityType}_template.csv");
    }

    /// <summary>
    /// Preview the first rows of an uploaded CSV file before importing.
    /// </summary>
    [HttpPost("preview")]
    [Authorize(Policy = Permissions.System.ImportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewImport([FromBody] PreviewImportRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new PreviewImportQuery(request.FileId, request.EntityType), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Start an import job for the given entity type and uploaded CSV file.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Policy = Permissions.System.ImportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartImport([FromBody] StartImportRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new StartImportCommand(request.FileId, request.EntityType, request.ConflictMode, request.TargetTenantId), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get a paginated list of import jobs for the current user/tenant.
    /// </summary>
    [HttpGet("imports")]
    [Authorize(Policy = Permissions.System.ImportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImportJobs(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetImportJobsQuery(pageNumber, pageSize), ct);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get a single import job by ID.
    /// </summary>
    [HttpGet("imports/{id:guid}")]
    [Authorize(Policy = Permissions.System.ImportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImportJobById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetImportJobByIdQuery(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Get a signed download URL for the error report of a completed import job.
    /// </summary>
    [HttpGet("imports/{id:guid}/errors")]
    [Authorize(Policy = Permissions.System.ImportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImportErrorReport(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetImportErrorReportQuery(id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Delete an import job record.
    /// </summary>
    [HttpDelete("imports/{id:guid}")]
    [Authorize(Policy = Permissions.System.ImportData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImportJob(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteImportJobCommand(id), ct);
        return HandleResult(result);
    }
}

/// <summary>Request body for import preview.</summary>
public sealed record PreviewImportRequest(Guid FileId, string EntityType);

/// <summary>Request body for starting an import job.</summary>
public sealed record StartImportRequest(Guid FileId, string EntityType, ConflictMode ConflictMode, Guid? TargetTenantId = null);
