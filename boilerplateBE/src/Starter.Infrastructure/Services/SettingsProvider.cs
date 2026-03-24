using Starter.Application.Common.Interfaces;

namespace Starter.Infrastructure.Services;

internal sealed class SettingsProvider(
    ISettingsService settingsService,
    ICurrentUserService currentUserService) : ISettingsProvider
{
    public async Task<string> GetStringAsync(string key, string defaultValue = "", CancellationToken ct = default)
    {
        var value = await settingsService.GetValueAsync(key, currentUserService.TenantId, ct);
        return value ?? defaultValue;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue = 0, CancellationToken ct = default)
    {
        var value = await GetStringAsync(key, string.Empty, ct);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken ct = default)
    {
        var value = await GetStringAsync(key, string.Empty, ct);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }
}
