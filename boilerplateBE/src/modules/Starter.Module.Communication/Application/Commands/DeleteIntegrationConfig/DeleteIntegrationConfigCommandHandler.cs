using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.DeleteIntegrationConfig;

internal sealed class DeleteIntegrationConfigCommandHandler(
    CommunicationDbContext dbContext)
    : IRequestHandler<DeleteIntegrationConfigCommand, Result>
{
    public async Task<Result> Handle(
        DeleteIntegrationConfigCommand request,
        CancellationToken cancellationToken)
    {
        var config = await dbContext.IntegrationConfigs
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (config is null)
            return Result.Failure(CommunicationErrors.IntegrationConfigNotFound);

        dbContext.IntegrationConfigs.Remove(config);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
