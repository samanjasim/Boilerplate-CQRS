using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.DeleteTemplateOverride;

internal sealed class DeleteTemplateOverrideCommandHandler(
    CommunicationDbContext dbContext)
    : IRequestHandler<DeleteTemplateOverrideCommand, Result>
{
    public async Task<Result> Handle(
        DeleteTemplateOverrideCommand request,
        CancellationToken cancellationToken)
    {
        var templateOverride = await dbContext.MessageTemplateOverrides
            .FirstOrDefaultAsync(o => o.MessageTemplateId == request.MessageTemplateId,
                cancellationToken);

        if (templateOverride is null)
            return Result.Failure(CommunicationErrors.TemplateOverrideNotFound);

        dbContext.MessageTemplateOverrides.Remove(templateOverride);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
