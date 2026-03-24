namespace Starter.Application.Common.Interfaces;

public interface ISettingsProvider
{
    Task<string> GetStringAsync(string key, string defaultValue = "", CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int defaultValue = 0, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken ct = default);
}
