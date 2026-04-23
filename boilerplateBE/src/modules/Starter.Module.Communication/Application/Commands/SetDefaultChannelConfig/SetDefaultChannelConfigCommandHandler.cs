using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.SetDefaultChannelConfig;

internal sealed class SetDefaultChannelConfigCommandHandler(
    CommunicationDbContext dbContext)
    : IRequestHandler<SetDefaultChannelConfigCommand, Result<ChannelConfigDto>>
{
    public async Task<Result<ChannelConfigDto>> Handle(
        SetDefaultChannelConfigCommand request,
        CancellationToken cancellationToken)
    {
        var config = await dbContext.ChannelConfigs
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (config is null)
            return Result.Failure<ChannelConfigDto>(CommunicationErrors.ChannelConfigNotFound);

        // Unset all existing defaults for this channel
        var currentDefaults = await dbContext.ChannelConfigs
            .Where(c => c.Channel == config.Channel && c.IsDefault && c.Id != config.Id)
            .ToListAsync(cancellationToken);

        foreach (var cd in currentDefaults)
            cd.SetDefault(false);

        config.SetDefault(true);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(config.ToDto());
    }
}
