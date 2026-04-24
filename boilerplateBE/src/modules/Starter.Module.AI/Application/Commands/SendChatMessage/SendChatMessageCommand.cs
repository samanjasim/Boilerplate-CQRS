using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.SendChatMessage;

public sealed record SendChatMessageCommand(
    Guid? ConversationId,
    Guid? AssistantId,
    string Message,
    Guid? PersonaId = null) : IRequest<Result<AiChatReplyDto>>;
