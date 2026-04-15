using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeleteAssistant;

internal sealed class DeleteAssistantCommandHandler(AiDbContext context)
    : IRequestHandler<DeleteAssistantCommand, Result>
{
    public async Task<Result> Handle(
        DeleteAssistantCommand request,
        CancellationToken cancellationToken)
    {
        var assistant = await context.AiAssistants
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (assistant is null)
            return Result.Failure(AiErrors.AssistantNotFound);

        var inUse = await context.AiConversations
            .AnyAsync(c => c.AssistantId == assistant.Id, cancellationToken);
        if (inUse)
            return Result.Failure(AiErrors.AssistantInUse);

        context.AiAssistants.Remove(assistant);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
