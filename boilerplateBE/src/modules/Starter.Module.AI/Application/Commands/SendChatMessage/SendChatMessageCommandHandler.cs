using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.SendChatMessage;

internal sealed class SendChatMessageCommandHandler(
    IChatExecutionService chat)
    : IRequestHandler<SendChatMessageCommand, Result<AiChatReplyDto>>
{
    public Task<Result<AiChatReplyDto>> Handle(SendChatMessageCommand request, CancellationToken cancellationToken) =>
        chat.ExecuteAsync(request.ConversationId, request.AssistantId, request.Message, request.PersonaId, cancellationToken);
}
