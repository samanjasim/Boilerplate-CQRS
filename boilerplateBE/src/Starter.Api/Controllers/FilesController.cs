using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Constants;
using Starter.Application.Features.Access.Commands.GrantResourceAccess;
using Starter.Application.Features.Access.Commands.RevokeResourceAccess;
using Starter.Application.Features.Access.Commands.SetResourceVisibility;
using Starter.Application.Features.Access.Commands.TransferResourceOwnership;
using Starter.Application.Features.Access.Queries.ListResourceGrants;
using Starter.Application.Features.Files;
using Starter.Application.Features.Files.Commands.DeleteFile;
using Starter.Application.Features.Files.Commands.UpdateFileMetadata;
using Starter.Application.Features.Files.Commands.UploadFile;
using Starter.Application.Features.Files.Queries.GetFileById;
using Starter.Application.Features.Files.Queries.GetFiles;
using Starter.Application.Features.Files.Queries.GetFileUrl;
using Starter.Domain.Common.Access.Enums;
using Starter.Domain.Common.Enums;
using Starter.Shared.Constants;
using Starter.Shared.Models;
using Starter.Shared.Results;

namespace Starter.Api.Controllers;

/// <summary>
/// File storage endpoints.
/// </summary>
public sealed class FilesController(ISender mediator, IFileService fileService, ISettingsProvider settingsProvider) : BaseApiController(mediator)
{
    /// <summary>
    /// Upload a file.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Files.Upload)]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] FileCategory category = FileCategory.Other,
        [FromForm] string? description = null,
        [FromForm] string? tags = null,
        [FromForm] string? entityType = null,
        [FromForm] Guid? entityId = null,
        [FromForm] ResourceVisibility visibility = ResourceVisibility.Private,
        CancellationToken cancellationToken = default)
    {
        using var stream = file.OpenReadStream();
        var maxSizeMb = await settingsProvider.GetIntAsync(FileSettings.MaxUploadSizeMbKey, FileSettings.MaxUploadSizeMbDefault);
        if (file.Length > maxSizeMb * 1024L * 1024L)
            return BadRequest(ApiResponse<object>.Fail($"File size exceeds the maximum allowed size of {maxSizeMb}MB."));
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
            visibility);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Upload a temporary file.
    /// </summary>
    [HttpPost("upload-temp")]
    [Authorize]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadTemporary(
        IFormFile file,
        [FromForm] string? description = null,
        CancellationToken cancellationToken = default)
    {
        using var stream = file.OpenReadStream();
        var maxSizeMb = await settingsProvider.GetIntAsync(FileSettings.MaxUploadSizeMbKey, FileSettings.MaxUploadSizeMbDefault);
        if (file.Length > maxSizeMb * 1024L * 1024L)
            return BadRequest(ApiResponse<object>.Fail($"File size exceeds the maximum allowed size of {maxSizeMb}MB."));
        var metadata = await fileService.UploadTemporaryAsync(
            stream, file.FileName, file.ContentType, file.Length,
            FileCategory.Other, description, null, cancellationToken);
        return HandleResult(Result.Success(metadata.ToDto()));
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

    /// <summary>
    /// List grants for a file.
    /// </summary>
    [HttpGet("{id:guid}/grants")]
    [Authorize(Policy = Permissions.Files.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListGrants(Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new ListResourceGrantsQuery(ResourceTypes.File, id), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Grant access to a file.
    /// </summary>
    [HttpPost("{id:guid}/grants")]
    [Authorize(Policy = Permissions.Files.ShareOwn)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GrantAccess(Guid id, [FromBody] GrantFileAccessRequest request, CancellationToken ct)
    {
        var command = new GrantResourceAccessCommand(
            ResourceTypes.File,
            id,
            request.SubjectType,
            request.SubjectId,
            request.Level);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Revoke a grant on a file.
    /// </summary>
    [HttpDelete("{id:guid}/grants/{grantId:guid}")]
    [Authorize(Policy = Permissions.Files.ShareOwn)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeGrant(Guid id, Guid grantId, CancellationToken ct)
    {
        var result = await Mediator.Send(new RevokeResourceAccessCommand(grantId), ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Set file visibility.
    /// </summary>
    [HttpPut("{id:guid}/visibility")]
    [Authorize(Policy = Permissions.Files.ShareOwn)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetVisibility(Guid id, [FromBody] SetFileVisibilityRequest request, CancellationToken ct)
    {
        var command = new SetResourceVisibilityCommand(ResourceTypes.File, id, request.Visibility);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    /// <summary>
    /// Transfer file ownership.
    /// </summary>
    [HttpPost("{id:guid}/transfer-ownership")]
    [Authorize(Policy = Permissions.Files.ShareOwn)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TransferOwnership(Guid id, [FromBody] TransferFileOwnershipRequest request, CancellationToken ct)
    {
        var command = new TransferResourceOwnershipCommand(ResourceTypes.File, id, request.NewOwnerId);
        var result = await Mediator.Send(command, ct);
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

/// <summary>
/// Request body for granting file access.
/// </summary>
public sealed record GrantFileAccessRequest(
    GrantSubjectType SubjectType,
    Guid SubjectId,
    AccessLevel Level);

/// <summary>
/// Request body for setting file visibility.
/// </summary>
public sealed record SetFileVisibilityRequest(ResourceVisibility Visibility);

/// <summary>
/// Request body for transferring file ownership.
/// </summary>
public sealed record TransferFileOwnershipRequest(Guid NewOwnerId);
