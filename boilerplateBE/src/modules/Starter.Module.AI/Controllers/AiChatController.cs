using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Starter.Module.AI.Application.Commands.DeleteConversation;
using Starter.Module.AI.Application.Commands.SendChatMessage;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Queries.GetConversationById;
using Starter.Module.AI.Application.Queries.GetConversations;
using Starter.Module.AI.Application.Services;
using Starter.Module.AI.Constants;
using Starter.Shared.Models;
using Starter.Shared.Results;

namespace Starter.Module.AI.Controllers;

[Route("api/v{version:apiVersion}/ai")]
public sealed class AiChatController(
    ISender mediator,
    IChatExecutionService chat,
    IValidator<SendChatMessageCommand> chatValidator)
    : Starter.Abstractions.Web.BaseApiController(mediator)
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("chat")]
    [Authorize(Policy = AiPermissions.Chat)]
    [ProducesResponseType(typeof(ApiResponse<AiChatReplyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Send([FromBody] SendChatMessageCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPost("chat/stream")]
    [Authorize(Policy = AiPermissions.Chat)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task StreamChat([FromBody] SendChatMessageCommand command, CancellationToken ct = default)
    {
        // Enforce the same validator as POST /chat before any SSE headers/frames are emitted.
        // Once text/event-stream headers are flushed, the status code can no longer be changed.
        var validationResult = await chatValidator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var validationErrors = ValidationErrors.FromErrors(
                validationResult.Errors.Select(f => new ValidationError(f.PropertyName, f.ErrorMessage)));

            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(ApiResponse.Fail(validationErrors), ct);
            return;
        }

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // disable Nginx buffering

        await foreach (var evt in chat.ExecuteStreamAsync(command.ConversationId, command.AssistantId, command.Message, command.PersonaId, ct))
        {
            var json = JsonSerializer.Serialize(new { type = evt.Type, data = evt.Data }, StreamJsonOptions);
            await Response.WriteAsync($"event: {evt.Type}\n", ct);
            await Response.WriteAsync($"data: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);
            if (evt.Type is "done" or "error") break;
        }
    }

    [HttpGet("conversations")]
    [Authorize(Policy = AiPermissions.ViewConversations)]
    [ProducesResponseType(typeof(PagedApiResponse<AiConversationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListConversations(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? assistantId = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetConversationsQuery(pageNumber, pageSize, searchTerm, assistantId), ct);
        return HandlePagedResult(result);
    }

    [HttpGet("conversations/{id:guid}")]
    [Authorize(Policy = AiPermissions.ViewConversations)]
    [ProducesResponseType(typeof(ApiResponse<AiConversationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversation(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetConversationByIdQuery(id), ct);
        return HandleResult(result);
    }

    [HttpDelete("conversations/{id:guid}")]
    [Authorize(Policy = AiPermissions.DeleteConversation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConversation(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteConversationCommand(id), ct);
        return HandleResult(result);
    }
}
