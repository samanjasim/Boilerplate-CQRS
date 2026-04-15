using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeleteConversation;

internal sealed class DeleteConversationCommandHandler(
    AiDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteConversationCommand, Result>
{
    public async Task<Result> Handle(DeleteConversationCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null)
            return Result.Failure(UserErrors.Unauthorized());

        var conversation = await context.AiConversations
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (conversation is null)
            return Result.Failure(AiErrors.ConversationNotFound);

        // Return NotFound (not Forbidden) to avoid leaking existence of other users' conversations.
        if (conversation.UserId != userId.Value)
            return Result.Failure(AiErrors.ConversationNotFound);

        // Server-side cascade — ExecuteDeleteAsync issues a single DELETE per table instead of
        // loading every message into the change tracker. Matters for conversations with many messages.
        await context.AiMessages
            .Where(m => m.ConversationId == conversation.Id)
            .ExecuteDeleteAsync(cancellationToken);

        context.AiConversations.Remove(conversation);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
