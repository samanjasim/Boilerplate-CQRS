using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Abstractions.Ai;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class SafetyProfileResolverTests
{
    private static (AiDbContext db, Mock<ICacheService> cache) Make()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options;
        var db = new AiDbContext(opts, cu.Object);
        // Default Moq behaviour: unset generic GetAsync<T> returns Task.FromResult<T>(default),
        // i.e. a cache miss for reference types — which is exactly what we want every test to
        // exercise (DB / fallback path). SetAsync / RemoveByPrefixAsync are no-ops.
        var cache = new Mock<ICacheService>();
        return (db, cache);
    }

    private static AiAssistant Assistant(SafetyPreset? overridePreset = null, Guid? tenantId = null)
    {
        var a = AiAssistant.Create(
            tenantId: tenantId ?? Guid.NewGuid(),
            name: "x",
            description: null,
            systemPrompt: "x",
            createdByUserId: Guid.NewGuid());
        if (overridePreset is { } p) a.SetSafetyPreset(p);
        return a;
    }

    private static SafetyProfileResolver Resolver(
        AiDbContext db,
        ICacheService cache,
        SafetyPreset tenantDefault = SafetyPreset.Standard)
    {
        return new SafetyProfileResolver(
            db,
            cache,
            new StaticTenantSettingsResolver(tenantDefault));
    }

    [Fact]
    public async Task Override_Preset_Wins_Over_Persona()
    {
        var (db, cache) = Make();
        var resolver = Resolver(db, cache.Object);

        var a = Assistant(SafetyPreset.ChildSafe);
        var resolved = await resolver.ResolveAsync(
            tenantId: a.TenantId,
            assistant: a,
            personaPreset: SafetyPreset.Standard,
            provider: ModerationProvider.OpenAi,
            ct: default);

        resolved.Preset.Should().Be(SafetyPreset.ChildSafe);
        // ChildSafe fallback fails closed.
        resolved.FailureMode.Should().Be(ModerationFailureMode.FailClosed);
    }

    [Fact]
    public async Task Tenant_Row_Wins_Over_Platform()
    {
        var (db, cache) = Make();
        var tenantId = Guid.NewGuid();

        // Platform default — looser threshold, fail-open.
        db.AiSafetyPresetProfiles.Add(AiSafetyPresetProfile.Create(
            tenantId: null,
            preset: SafetyPreset.Standard,
            provider: ModerationProvider.OpenAi,
            thresholdsJson: """{"sexual":0.85}""",
            blockedCategoriesJson: "[]",
            failureMode: ModerationFailureMode.FailOpen,
            redactPii: false));

        // Tenant override — tighter threshold, fail-closed.
        db.AiSafetyPresetProfiles.Add(AiSafetyPresetProfile.Create(
            tenantId: tenantId,
            preset: SafetyPreset.Standard,
            provider: ModerationProvider.OpenAi,
            thresholdsJson: """{"sexual":0.5}""",
            blockedCategoriesJson: "[]",
            failureMode: ModerationFailureMode.FailClosed,
            redactPii: false));

        await db.SaveChangesAsync();

        var resolver = Resolver(db, cache.Object);
        var assistant = AiAssistant.Create(tenantId, "x", null, "x", Guid.NewGuid());

        var resolved = await resolver.ResolveAsync(
            tenantId, assistant, personaPreset: null, ModerationProvider.OpenAi, ct: default);

        resolved.CategoryThresholds["sexual"].Should().Be(0.5);
        resolved.FailureMode.Should().Be(ModerationFailureMode.FailClosed);
    }

    [Fact]
    public async Task Falls_Back_To_Hard_Coded_When_No_Rows()
    {
        var (db, cache) = Make();
        var resolver = Resolver(db, cache.Object);
        var assistant = AiAssistant.Create(Guid.NewGuid(), "x", null, "x", Guid.NewGuid());

        var resolved = await resolver.ResolveAsync(
            assistant.TenantId, assistant,
            personaPreset: SafetyPreset.ChildSafe,
            provider: ModerationProvider.OpenAi,
            ct: default);

        // Canonical OpenAI wire-format key — slashes, not hyphens — must match seed + moderator.
        resolved.BlockedCategories.Should().Contain("sexual/minors");
        resolved.FailureMode.Should().Be(ModerationFailureMode.FailClosed);
    }

    [Fact]
    public async Task SafetyResolver_Uses_Tenant_Default_When_Assistant_And_Persona_Missing()
    {
        var (db, cache) = Make();
        var tenantId = Guid.NewGuid();
        var resolver = Resolver(db, cache.Object, SafetyPreset.ProfessionalModerated);
        var assistant = Assistant(tenantId: tenantId);

        var resolved = await resolver.ResolveAsync(
            tenantId,
            assistant,
            personaPreset: null,
            provider: ModerationProvider.OpenAi,
            ct: default);

        resolved.Preset.Should().Be(SafetyPreset.ProfessionalModerated);
    }

    [Fact]
    public async Task SafetyResolver_Assistant_Override_Wins_Over_Tenant_Default()
    {
        var (db, cache) = Make();
        var tenantId = Guid.NewGuid();
        var resolver = Resolver(db, cache.Object, SafetyPreset.ProfessionalModerated);
        var assistant = Assistant(SafetyPreset.ChildSafe, tenantId);

        var resolved = await resolver.ResolveAsync(
            tenantId,
            assistant,
            personaPreset: null,
            provider: ModerationProvider.OpenAi,
            ct: default);

        resolved.Preset.Should().Be(SafetyPreset.ChildSafe);
    }

    private sealed class StaticTenantSettingsResolver(SafetyPreset defaultPreset) : IAiTenantSettingsResolver
    {
        public Task<AiTenantSettings> GetOrDefaultAsync(Guid tenantId, CancellationToken ct = default)
        {
            var settings = AiTenantSettings.CreateDefault(tenantId);
            settings.UpdatePolicy(ProviderCredentialPolicy.PlatformOnly, defaultPreset);
            return Task.FromResult(settings);
        }

        public Task<ProviderCredentialPolicy> ResolveEffectivePolicyAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(ProviderCredentialPolicy.PlatformOnly);
    }
}
