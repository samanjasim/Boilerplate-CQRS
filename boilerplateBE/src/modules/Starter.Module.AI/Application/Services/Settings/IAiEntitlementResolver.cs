using Starter.Module.AI.Application.DTOs;

namespace Starter.Module.AI.Application.Services.Settings;

internal interface IAiEntitlementResolver
{
    Task<AiEntitlementsDto> ResolveAsync(CancellationToken ct = default);
    Task<AiEntitlementsDto> ResolveAsync(Guid? tenantId, CancellationToken ct = default);
}
