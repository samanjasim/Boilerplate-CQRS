using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.DeleteChannelConfig;

internal sealed class DeleteChannelConfigCommandHandler(
    CommunicationDbContext dbContext)
    : IRequestHandler<DeleteChannelConfigCommand, Result>
{
    public async Task<Result> Handle(
        DeleteChannelConfigCommand request,
        CancellationToken cancellationToken)
    {
        var config = await dbContext.ChannelConfigs
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (config is null)
            return Result.Failure(CommunicationErrors.ChannelConfigNotFound);

        dbContext.ChannelConfigs.Remove(config);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
