using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds the three platform-default `AiSafetyPresetProfile` rows (one per `SafetyPreset`).
/// Idempotent — skips if any platform-default row (`TenantId == null`) already exists.
/// </summary>
public static class SafetyPresetProfileSeed
{
    public static async Task SeedAsync(AiDbContext db, CancellationToken ct = default)
    {
        var any = await db.AiSafetyPresetProfiles
            .IgnoreQueryFilters()
            .AnyAsync(p => p.TenantId == null, ct);
        if (any) return;

        const string standardThresholds =
            """{"sexual":0.85,"hate":0.85,"violence":0.85,"self-harm":0.85,"harassment":0.85}""";
        const string childSafeThresholds =
            """{"sexual":0.5,"hate":0.5,"violence":0.5,"self-harm":0.3,"harassment":0.5}""";
        const string emptyBlocked = "[]";
        const string childSafeBlocked =
            """["sexual/minors","violence/graphic"]""";

        db.AiSafetyPresetProfiles.AddRange(
            AiSafetyPresetProfile.Create(
                tenantId: null,
                preset: SafetyPreset.Standard,
                provider: ModerationProvider.OpenAi,
                thresholdsJson: standardThresholds,
                blockedCategoriesJson: emptyBlocked,
                failureMode: ModerationFailureMode.FailOpen,
                redactPii: false),
            AiSafetyPresetProfile.Create(
                tenantId: null,
                preset: SafetyPreset.ChildSafe,
                provider: ModerationProvider.OpenAi,
                thresholdsJson: childSafeThresholds,
                blockedCategoriesJson: childSafeBlocked,
                failureMode: ModerationFailureMode.FailClosed,
                redactPii: false),
            AiSafetyPresetProfile.Create(
                tenantId: null,
                preset: SafetyPreset.ProfessionalModerated,
                provider: ModerationProvider.OpenAi,
                thresholdsJson: standardThresholds,
                blockedCategoriesJson: emptyBlocked,
                failureMode: ModerationFailureMode.FailClosed,
                redactPii: true)
        );

        await db.SaveChangesAsync(ct);
    }
}
