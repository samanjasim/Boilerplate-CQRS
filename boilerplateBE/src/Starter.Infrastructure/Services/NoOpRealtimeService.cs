using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Services;

public sealed class NoOpRealtimeService(ILogger<NoOpRealtimeService> logger) : IRealtimeService
{
    public Task PublishToUserAsync(Guid userId, string eventName, object data, CancellationToken ct = default)
    {
        logger.LogDebug(
            "Ably disabled — skipping realtime publish to user {UserId}, event {EventName}",
            userId, eventName);
        return Task.CompletedTask;
    }

    public Task PublishToChannelAsync(string channel, string eventName, object data, CancellationToken ct = default)
        => Task.CompletedTask;
}
