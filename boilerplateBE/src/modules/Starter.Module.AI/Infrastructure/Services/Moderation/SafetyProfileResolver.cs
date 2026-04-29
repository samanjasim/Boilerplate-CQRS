using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;

namespace Starter.Module.AI.Infrastructure.Services.Moderation;

/// <summary>
/// Default <see cref="ISafetyProfileResolver"/> backed by <see cref="AiDbContext"/> and
/// <see cref="ICacheService"/> (60s TTL, prefix-based invalidation).
///
/// Hard-coded fallback profiles use canonical OpenAI wire-format category keys
/// (e.g. <c>sexual/minors</c>, <c>violence/graphic</c>) so they line up with what
/// <c>OpenAiContentModerator</c> emits and the seeded platform defaults.
/// </summary>
internal sealed class SafetyProfileResolver(
    AiDbContext db,
    ICacheService cache,
    IAiTenantSettingsResolver tenantSettings) : ISafetyProfileResolver
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public async Task<ResolvedSafetyProfile> ResolveAsync(
        Guid? tenantId,
        AiAssistant assistant,
        SafetyPreset? personaPreset,
        ModerationProvider provider,
        CancellationToken ct)
    {
        var tenantDefault = tenantId is { } tid
            ? (await tenantSettings.GetOrDefaultAsync(tid, ct)).DefaultSafetyPreset
            : SafetyPreset.Standard;
        var preset = assistant.SafetyPresetOverride ?? personaPreset ?? tenantDefault;
        var key = $"safety:profile:{tenantId?.ToString() ?? "platform"}:{preset}:{provider}";

        var cached = await cache.GetAsync<CachedProfile>(key, ct);
        if (cached is not null)
            return cached.ToResolved(preset, provider);

        var resolved = await LoadFromDbAsync(tenantId, preset, provider, ct);
        await cache.SetAsync(key, CachedProfile.From(resolved), Ttl, ct);
        return resolved;
    }

    public async Task InvalidateAsync(Guid? tenantId, CancellationToken ct)
    {
        var prefix = $"safety:profile:{tenantId?.ToString() ?? "platform"}:";
        await cache.RemoveByPrefixAsync(prefix, ct);
    }

    private async Task<ResolvedSafetyProfile> LoadFromDbAsync(
        Guid? tenantId, SafetyPreset preset, ModerationProvider provider, CancellationToken ct)
    {
        // Tenant override → platform default → hard-coded fallback.
        AiSafetyPresetProfile? row = null;
        if (tenantId is { } tid)
        {
            row = await db.AiSafetyPresetProfiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.TenantId == tid && p.Preset == preset && p.Provider == provider && p.IsActive, ct);
        }
        row ??= await db.AiSafetyPresetProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.TenantId == null && p.Preset == preset && p.Provider == provider && p.IsActive, ct);

        if (row is null) return Fallback(preset, provider);

        var thresholds = JsonSerializer.Deserialize<Dictionary<string, double>>(row.CategoryThresholdsJson)
                         ?? new Dictionary<string, double>();
        var blocked = JsonSerializer.Deserialize<List<string>>(row.BlockedCategoriesJson) ?? new List<string>();
        return new ResolvedSafetyProfile(preset, provider, thresholds, blocked, row.FailureMode, row.RedactPii);
    }

    private static ResolvedSafetyProfile Fallback(SafetyPreset preset, ModerationProvider provider) =>
        preset switch
        {
            SafetyPreset.ChildSafe => new(
                preset, provider,
                new Dictionary<string, double>
                {
                    ["sexual"] = 0.5,
                    ["hate"] = 0.5,
                    ["violence"] = 0.5,
                    ["self-harm"] = 0.3,
                    ["harassment"] = 0.5,
                },
                // Canonical OpenAI wire-format keys (slashes, not hyphens) — must match
                // what OpenAiContentModerator emits and what the platform seed inserts.
                new[] { "sexual/minors", "violence/graphic" },
                ModerationFailureMode.FailClosed,
                RedactPii: false),
            SafetyPreset.ProfessionalModerated => new(
                preset, provider,
                new Dictionary<string, double>
                {
                    ["sexual"] = 0.85,
                    ["hate"] = 0.85,
                    ["violence"] = 0.85,
                    ["self-harm"] = 0.85,
                    ["harassment"] = 0.85,
                },
                Array.Empty<string>(),
                ModerationFailureMode.FailClosed,
                RedactPii: true),
            _ => new(
                preset, provider,
                new Dictionary<string, double>
                {
                    ["sexual"] = 0.85,
                    ["hate"] = 0.85,
                    ["violence"] = 0.85,
                    ["self-harm"] = 0.85,
                    ["harassment"] = 0.85,
                },
                Array.Empty<string>(),
                ModerationFailureMode.FailOpen,
                RedactPii: false),
        };

    private sealed record CachedProfile(
        Dictionary<string, double> Thresholds,
        List<string> BlockedCategories,
        ModerationFailureMode FailureMode,
        bool RedactPii)
    {
        public static CachedProfile From(ResolvedSafetyProfile p) => new(
            new Dictionary<string, double>(p.CategoryThresholds),
            p.BlockedCategories.ToList(),
            p.FailureMode,
            p.RedactPii);

        public ResolvedSafetyProfile ToResolved(SafetyPreset preset, ModerationProvider provider) =>
            new(preset, provider, Thresholds, BlockedCategories, FailureMode, RedactPii);
    }
}
