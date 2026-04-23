using MediatR;
using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Application.DTOs;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Errors;
using Starter.Module.Communication.Infrastructure.Persistence;
using Starter.Module.Communication.Infrastructure.Services;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.CreateChannelConfig;

internal sealed class CreateChannelConfigCommandHandler(
    CommunicationDbContext dbContext,
    ICurrentUserService currentUserService,
    ICredentialEncryptionService encryptionService)
    : IRequestHandler<CreateChannelConfigCommand, Result<ChannelConfigDto>>
{
    public async Task<Result<ChannelConfigDto>> Handle(
        CreateChannelConfigCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;
        if (!tenantId.HasValue)
            return Result.Failure<ChannelConfigDto>(CommunicationErrors.TenantRequired);

        // Validate channel+provider combination
        if (!IsValidChannelProviderCombination(request.Channel, request.Provider))
            return Result.Failure<ChannelConfigDto>(CommunicationErrors.InvalidChannelProviderCombination);

        // Check for duplicate channel+provider config
        var exists = await dbContext.ChannelConfigs
            .AnyAsync(c => c.Channel == request.Channel && c.Provider == request.Provider,
                cancellationToken);
        if (exists)
            return Result.Failure<ChannelConfigDto>(CommunicationErrors.DuplicateChannelConfig);

        // If setting as default, unset any existing default for this channel
        if (request.IsDefault)
        {
            var currentDefaults = await dbContext.ChannelConfigs
                .Where(c => c.Channel == request.Channel && c.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var cd in currentDefaults)
                cd.SetDefault(false);
        }

        var encryptedCredentials = encryptionService.Encrypt(request.Credentials);

        var config = ChannelConfig.Create(
            tenantId.Value,
            request.Channel,
            request.Provider,
            request.DisplayName,
            encryptedCredentials,
            request.IsDefault);

        dbContext.ChannelConfigs.Add(config);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(config.ToDto());
    }

    private static bool IsValidChannelProviderCombination(
        Domain.Enums.NotificationChannel channel,
        Domain.Enums.ChannelProvider provider) => (channel, provider) switch
    {
        (Domain.Enums.NotificationChannel.Email, Domain.Enums.ChannelProvider.Smtp) => true,
        (Domain.Enums.NotificationChannel.Email, Domain.Enums.ChannelProvider.SendGrid) => true,
        (Domain.Enums.NotificationChannel.Email, Domain.Enums.ChannelProvider.Ses) => true,
        (Domain.Enums.NotificationChannel.Sms, Domain.Enums.ChannelProvider.Twilio) => true,
        (Domain.Enums.NotificationChannel.Push, Domain.Enums.ChannelProvider.Fcm) => true,
        (Domain.Enums.NotificationChannel.Push, Domain.Enums.ChannelProvider.Apns) => true,
        (Domain.Enums.NotificationChannel.WhatsApp, Domain.Enums.ChannelProvider.TwilioWhatsApp) => true,
        (Domain.Enums.NotificationChannel.WhatsApp, Domain.Enums.ChannelProvider.MetaWhatsApp) => true,
        (Domain.Enums.NotificationChannel.InApp, Domain.Enums.ChannelProvider.Ably) => true,
        _ => false,
    };
}
