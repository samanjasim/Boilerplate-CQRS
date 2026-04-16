using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetChannelConfigById;

internal sealed class GetChannelConfigByIdQueryHandler(
    CommunicationDbContext context,
    ICredentialEncryptionService encryptionService)
    : IRequestHandler<GetChannelConfigByIdQuery, Result<ChannelConfigDetailDto>>
{
    public async Task<Result<ChannelConfigDetailDto>> Handle(
        GetChannelConfigByIdQuery request,
        CancellationToken cancellationToken)
    {
        var config = await context.ChannelConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (config is null)
            return Result.Failure<ChannelConfigDetailDto>(CommunicationErrors.ChannelConfigNotFound);

        var credentials = encryptionService.Decrypt(config.CredentialsJson);
        var maskedCredentials = encryptionService.Mask(credentials);

        return Result.Success(config.ToDetailDto(maskedCredentials));
    }
}
