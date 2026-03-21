namespace Starter.Application.Common.Interfaces;

public interface IRealtimeService
{
    Task PublishToUserAsync(Guid userId, string eventName, object data, CancellationToken ct = default);
}
