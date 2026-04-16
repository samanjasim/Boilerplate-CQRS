using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.DeleteTriggerRule;

internal sealed class DeleteTriggerRuleCommandHandler(
    CommunicationDbContext dbContext)
    : IRequestHandler<DeleteTriggerRuleCommand, Result>
{
    public async Task<Result> Handle(
        DeleteTriggerRuleCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.TriggerRules
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (entity is null)
            return Result.Failure(CommunicationErrors.TriggerRuleNotFound);

        dbContext.TriggerRules.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
