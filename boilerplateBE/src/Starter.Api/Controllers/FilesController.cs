using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Features.Files;
using Starter.Application.Features.Files.Commands.DeleteFile;
using Starter.Application.Features.Files.Commands.UpdateFileMetadata;
using Starter.Application.Features.Files.Commands.UploadFile;
using Starter.Application.Features.Files.Queries.GetFileById;
using Starter.Application.Features.Files.Queries.GetFiles;
using Starter.Application.Features.Files.Queries.GetFileUrl;
using Starter.Domain.Common.Enums;
using Starter.Shared.Constants;

namespace Starter.Api.Controllers;

/// <summary>
/// File storage endpoints.
/// </summary>
public sealed class FilesController(ISender mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Upload a file.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Files.Upload)]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] FileCategory category = FileCategory.Other,
        [FromForm] string? description = null,
        [FromForm] string? tags = null,
        [FromForm] string? entityType = null,
        [FromForm] Guid? entityId = null,
        [FromForm] bool isPublic = false)
    {
        var stream = file.OpenReadStream();
        var command = new UploadFileCommand(
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            category,
            description,
            tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            entityType,
            entityId,
            isPublic);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Get paginated list of files.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Files.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFiles([FromQuery] GetFilesQuery query)
    {
        var result = await Mediator.Send(query);
        return HandlePagedResult(result);
    }

    /// <summary>
    /// Get file details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Files.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFileById(Guid id)
    {
        var result = await Mediator.Send(new GetFileByIdQuery(id));
        return HandleResult(result);
    }

    /// <summary>
    /// Get a signed URL for downloading a file.
    /// </summary>
    [HttpGet("{id:guid}/url")]
    [Authorize(Policy = Permissions.Files.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFileUrl(Guid id)
    {
        var result = await Mediator.Send(new GetFileUrlQuery(id));
        return HandleResult(result);
    }

    /// <summary>
    /// Update file metadata.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Files.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(Guid id, [FromBody] UpdateFileMetadataRequest request)
    {
        var command = new UpdateFileMetadataCommand(id, request.Description, request.Category, request.Tags);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Delete a file.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Files.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await Mediator.Send(new DeleteFileCommand(id));
        return HandleResult(result);
    }
}

/// <summary>
/// Request body for updating file metadata.
/// </summary>
public sealed record UpdateFileMetadataRequest(
    string? Description,
    FileCategory? Category,
    string[]? Tags);
