using Starter.Application.Common.Interfaces;

namespace Starter.Api.Tests.Access._Helpers;

public sealed class FakeNotificationService : INotificationService
{
    public List<(Guid UserId, string Type, string Title)> Sent { get; } = new();

    public Task CreateAsync(Guid userId, Guid? tenantId, string type, string title, string message, string? data = null, CancellationToken ct = default)
    {
        Sent.Add((userId, type, title));
        return Task.CompletedTask;
    }

    public Task CreateForTenantAdminsAsync(Guid tenantId, string type, string title, string message, string? data = null, CancellationToken ct = default)
        => Task.CompletedTask;
}
