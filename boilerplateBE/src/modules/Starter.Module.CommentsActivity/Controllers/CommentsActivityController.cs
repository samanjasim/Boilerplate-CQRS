using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Abstractions.Web;
using Starter.Module.CommentsActivity.Application.Commands.AddComment;
using Starter.Module.CommentsActivity.Application.Commands.DeleteComment;
using Starter.Module.CommentsActivity.Application.Commands.EditComment;
using Starter.Module.CommentsActivity.Application.Commands.ToggleReaction;
using Starter.Module.CommentsActivity.Application.Commands.UnwatchEntity;
using Starter.Module.CommentsActivity.Application.Commands.WatchEntity;
using Starter.Module.CommentsActivity.Application.DTOs;
using Starter.Module.CommentsActivity.Application.Queries.GetActivity;
using Starter.Module.CommentsActivity.Application.Queries.GetComments;
using Starter.Module.CommentsActivity.Application.Queries.GetMentionableUsers;
using Starter.Module.CommentsActivity.Application.Queries.GetTimeline;
using Starter.Module.CommentsActivity.Application.Queries.GetWatchStatus;
using Starter.Module.CommentsActivity.Constants;
using Starter.Shared.Models;

namespace Starter.Module.CommentsActivity.Controllers;

[RequireFeatureFlag("comments.activity_enabled")]
public sealed class CommentsActivityController(ISender mediator) : BaseApiController(mediator)
{
    [HttpGet("comments")]
    [Authorize(Policy = CommentsActivityPermissions.ViewComments)]
    [ProducesResponseType(typeof(PagedApiResponse<CommentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComments(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetCommentsQuery(entityType, entityId, pageNumber, pageSize), ct);
        return HandlePagedResult(result);
    }

    [HttpPost("comments")]
    [Authorize(Policy = CommentsActivityPermissions.CreateComments)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddComment(
        [FromBody] AddCommentCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPut("comments/{id:guid}")]
    [Authorize(Policy = CommentsActivityPermissions.EditComments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditComment(
        Guid id, [FromBody] EditCommentCommand command, CancellationToken ct = default)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("comments/{id:guid}")]
    [Authorize(Policy = CommentsActivityPermissions.DeleteComments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComment(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteCommentCommand(id), ct);
        return HandleResult(result);
    }

    // Reacting is a read-adjacent action: any user who can see the thread
    // should be able to react. We intentionally gate this behind ViewComments,
    // not CreateComments, so read-only roles can still react.
    [HttpPost("comments/{id:guid}/reactions")]
    [Authorize(Policy = CommentsActivityPermissions.ViewComments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleReaction(
        Guid id, [FromBody] ToggleReactionRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ToggleReactionCommand(id, request.ReactionType), ct);
        return HandleResult(result);
    }

    [HttpDelete("comments/{id:guid}/reactions/{reactionType}")]
    [Authorize(Policy = CommentsActivityPermissions.ViewComments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveReaction(
        Guid id, string reactionType, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ToggleReactionCommand(id, reactionType), ct);
        return HandleResult(result);
    }

    [HttpGet("activity")]
    [Authorize(Policy = CommentsActivityPermissions.ViewActivity)]
    [ProducesResponseType(typeof(PagedApiResponse<ActivityEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivity(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetActivityQuery(entityType, entityId, pageNumber, pageSize), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("timeline")]
    [Authorize(Policy = CommentsActivityPermissions.ViewComments)]
    [ProducesResponseType(typeof(PagedApiResponse<TimelineItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimeline(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        [FromQuery] string filter = "all",
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetTimelineQuery(entityType, entityId, filter, pageNumber, pageSize), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("watchers/status")]
    [Authorize(Policy = CommentsActivityPermissions.ViewComments)]
    [ProducesResponseType(typeof(ApiResponse<WatchStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWatchStatus(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWatchStatusQuery(entityType, entityId), ct);
        return HandleResult(result);
    }

    [HttpPost("watchers")]
    [Authorize(Policy = CommentsActivityPermissions.ViewComments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Watch(
        [FromBody] WatchEntityCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpDelete("watchers")]
    [Authorize(Policy = CommentsActivityPermissions.ViewComments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Unwatch(
        [FromQuery] string entityType,
        [FromQuery] Guid entityId,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new UnwatchEntityCommand(entityType, entityId), ct);
        return HandleResult(result);
    }

    [HttpGet("mentionable-users")]
    [Authorize(Policy = CommentsActivityPermissions.CreateComments)]
    [ProducesResponseType(typeof(ApiResponse<List<MentionableUserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMentionableUsers(
        [FromQuery] string? search,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new GetMentionableUsersQuery(search, pageSize, entityType, entityId), ct);
        return HandleResult(result);
    }
}

public sealed record ToggleReactionRequest(string ReactionType);
