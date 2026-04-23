using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetConversationById;

internal sealed class GetConversationByIdQueryHandler(
    AiDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<GetConversationByIdQuery, Result<AiConversationDetailDto>>
{
    public async Task<Result<AiConversationDetailDto>> Handle(
        GetConversationByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null)
            return Result.Failure<AiConversationDetailDto>(UserErrors.Unauthorized());

        var conversation = await context.AiConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (conversation is null)
            return Result.Failure<AiConversationDetailDto>(AiErrors.ConversationNotFound);

        // Return NotFound (not Forbidden) to avoid leaking existence of other users' conversations.
        if (conversation.UserId != userId.Value)
            return Result.Failure<AiConversationDetailDto>(AiErrors.ConversationNotFound);

        var messages = await context.AiMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id)
            .OrderBy(m => m.Order)
            .ToListAsync(cancellationToken);

        var assistantName = await context.AiAssistants
            .AsNoTracking()
            .Where(a => a.Id == conversation.AssistantId)
            .Select(a => (string?)a.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(conversation.ToDetailDto(messages, assistantName));
    }
}
