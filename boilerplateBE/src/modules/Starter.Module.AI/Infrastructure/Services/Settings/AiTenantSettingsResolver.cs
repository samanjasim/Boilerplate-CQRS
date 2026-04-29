using Microsoft.EntityFrameworkCore;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Services.Settings;

internal sealed class AiTenantSettingsResolver(
    AiDbContext db,
    IAiEntitlementResolver entitlements) : IAiTenantSettingsResolver
{
    public async Task<AiTenantSettings> GetOrDefaultAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await db.AiTenantSettings
                   .AsNoTracking()
                   .IgnoreQueryFilters()
                   .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct)
               ?? AiTenantSettings.CreateDefault(tenantId);
    }

    public async Task<ProviderCredentialPolicy> ResolveEffectivePolicyAsync(Guid tenantId, CancellationToken ct = default)
    {
        var settings = await GetOrDefaultAsync(tenantId, ct);
        var resolvedEntitlements = await entitlements.ResolveAsync(ct);
        return resolvedEntitlements.ByokEnabled
            ? settings.RequestedProviderCredentialPolicy
            : ProviderCredentialPolicy.PlatformOnly;
    }
}
