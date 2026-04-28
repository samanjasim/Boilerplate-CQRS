using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Moderation;

/// <summary>
/// Resolves the per-request safety profile that drives moderation decisions.
///
/// <para>Preset precedence (selects which <see cref="SafetyPreset"/> applies):</para>
/// <list type="number">
///   <item><description><c>assistant.SafetyPresetOverride</c> when non-null,</description></item>
///   <item><description>otherwise the persona's <c>SafetyPreset</c>,</description></item>
///   <item><description>otherwise <see cref="SafetyPreset.Standard"/>.</description></item>
/// </list>
///
/// <para>Threshold-row precedence (selects which <c>AiSafetyPresetProfile</c> row applies for the
/// chosen <c>(Preset, Provider)</c>):</para>
/// <list type="number">
///   <item><description>tenant-specific active row,</description></item>
///   <item><description>platform-default row (<c>TenantId == null</c>),</description></item>
///   <item><description>hard-coded fallback (<see cref="SafetyPreset"/>-specific defaults using
///   canonical OpenAI wire-format category keys).</description></item>
/// </list>
///
/// <para>Resolved profiles are cached for 60s under the key
/// <c>safety:profile:{tenantId|"platform"}:{preset}:{provider}</c>. Callers must invoke
/// <see cref="InvalidateAsync"/> (or rely on the dedicated <c>InvalidateSafetyProfileCacheOn*</c>
/// MediatR handlers) when a relevant tenant override or platform default changes; the prefix-based
/// invalidation flushes every cached preset×provider combination for the tenant in one call.</para>
/// </summary>
internal interface ISafetyProfileResolver
{
    /// <summary>
    /// Resolves the active safety profile for an agent run. Override precedence:
    /// <c>assistant.SafetyPresetOverride</c> &gt; <paramref name="personaPreset"/> &gt;
    /// <see cref="SafetyPreset.Standard"/>. Threshold profile precedence: tenant row &gt;
    /// platform row &gt; hard-coded fallback.
    /// </summary>
    Task<ResolvedSafetyProfile> ResolveAsync(
        Guid? tenantId,
        AiAssistant assistant,
        SafetyPreset? personaPreset,
        ModerationProvider provider,
        CancellationToken ct);

    /// <summary>
    /// Flushes every cached profile for the given tenant scope (and the platform scope when
    /// <paramref name="tenantId"/> is <c>null</c>). Removes by key prefix so all preset×provider
    /// combinations are evicted in one call.
    /// </summary>
    Task InvalidateAsync(Guid? tenantId, CancellationToken ct);
}
