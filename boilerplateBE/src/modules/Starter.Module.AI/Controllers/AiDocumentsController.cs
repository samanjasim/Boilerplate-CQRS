using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.DeleteDocument;
using Starter.Module.AI.Application.Commands.ReprocessDocument;
using Starter.Module.AI.Application.Commands.UploadDocument;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.GetDocumentById;
using Starter.Module.AI.Application.Queries.GetDocuments;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai/documents")]
public sealed class AiDocumentsController(ISender mediator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    [HttpGet]
    [Authorize(Policy = AiPermissions.ManageDocuments)]
    [ProducesResponseType(typeof(PagedApiResponse<AiDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetDocumentsQuery(pageNumber, pageSize, status, searchTerm), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManageDocuments)]
    [ProducesResponseType(typeof(ApiResponse<AiDocumentDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetDocumentByIdQuery(id), ct);
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Policy = AiPermissions.ManageDocuments)]
    [RequestSizeLimit(26_214_400)]
    [ProducesResponseType(typeof(ApiResponse<AiDocumentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? name,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new UploadDocumentCommand(file, name), ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AiPermissions.ManageDocuments)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteDocumentCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/reprocess")]
    [Authorize(Policy = AiPermissions.ManageDocuments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reprocess(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ReprocessDocumentCommand(id), ct);
        return HandleResult(result);
    }
}
