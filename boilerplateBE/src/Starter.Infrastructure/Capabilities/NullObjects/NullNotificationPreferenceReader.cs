using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;

namespace Starter.Infrastructure.Capabilities.NullObjects;

/// <summary>
/// Null implementation of <see cref="INotificationPreferenceReader"/>.
/// Returns <c>true</c> (email enabled) as the default — preferences are
/// always resolved from the real implementation in practice, but the Null
/// Object keeps isolated module tests compilable.
/// </summary>
public sealed class NullNotificationPreferenceReader(
    ILogger<NullNotificationPreferenceReader> logger) : INotificationPreferenceReader
{
    public Task<bool> IsEmailEnabledAsync(Guid userId, string notificationType, CancellationToken ct = default)
    {
        logger.LogDebug(
            "Notification preference check skipped — returning default enabled (user: {UserId}, type: {Type})",
            userId, notificationType);
        return Task.FromResult(true);
    }
}
