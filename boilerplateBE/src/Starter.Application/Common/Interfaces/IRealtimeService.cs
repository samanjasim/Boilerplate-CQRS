namespace Starter.Application.Common.Interfaces;

public interface IRealtimeService
{
    Task PublishToUserAsync(Guid userId, string eventName, object data, CancellationToken ct = default);
    Task PublishToChannelAsync(string channel, string eventName, object data, CancellationToken ct = default);
}
