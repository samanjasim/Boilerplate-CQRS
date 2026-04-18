using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Capabilities;
using Starter.Domain.Common;
using Starter.Infrastructure.Persistence;

namespace Starter.Infrastructure.Capabilities;

public sealed class NotificationPreferenceReaderService(
    ApplicationDbContext context) : INotificationPreferenceReader
{
    public async Task<bool> IsEmailEnabledAsync(
        Guid userId, string notificationType, CancellationToken ct = default)
    {
        var pref = await context.Set<NotificationPreference>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                np => np.UserId == userId && np.NotificationType == notificationType, ct);

        return pref?.EmailEnabled ?? true;
    }
}
