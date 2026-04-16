using Starter.Abstractions.Readers;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Infrastructure.Services;

public interface IRecipientResolver
{
    Task<string?> ResolveAddressAsync(Guid userId, NotificationChannel channel, CancellationToken ct = default);
}

internal sealed class RecipientResolver(IUserReader userReader) : IRecipientResolver
{
    public async Task<string?> ResolveAddressAsync(Guid userId, NotificationChannel channel, CancellationToken ct = default)
    {
        var user = await userReader.GetAsync(userId, ct);
        if (user is null) return null;

        return channel switch
        {
            NotificationChannel.Email => user.Email,
            NotificationChannel.InApp => user.Email, // Use email as identifier for in-app
            // SMS, Push, WhatsApp will need additional user profile fields (phone, device tokens)
            // For now, return null — these channels will be fully supported when user profile is extended
            _ => null,
        };
    }
}
