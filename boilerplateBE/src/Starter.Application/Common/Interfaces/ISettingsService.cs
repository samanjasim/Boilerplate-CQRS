using Starter.Application.Features.Settings.DTOs;

namespace Starter.Application.Common.Interfaces;

public interface ISettingsService
{
    Task<string?> GetValueAsync(string key, Guid? tenantId = null, CancellationToken ct = default);
    Task<List<SystemSettingDto>> GetAllAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task SetValueAsync(string key, string value, Guid? tenantId = null, CancellationToken ct = default);
}
