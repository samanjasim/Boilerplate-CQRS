using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Settings;

internal interface IAiTenantSettingsResolver
{
    Task<AiTenantSettings> GetOrDefaultAsync(Guid tenantId, CancellationToken ct = default);
    Task<ProviderCredentialPolicy> ResolveEffectivePolicyAsync(Guid tenantId, CancellationToken ct = default);
}
