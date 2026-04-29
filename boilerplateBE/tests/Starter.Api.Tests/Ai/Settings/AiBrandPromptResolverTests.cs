using FluentAssertions;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Settings;

public sealed class AiBrandPromptResolverTests
{
    [Fact]
    public async Task BrandPromptResolver_Returns_Empty_When_No_Brand_Profile()
    {
        var tenantId = Guid.NewGuid();
        var resolver = new AiBrandPromptResolver(new StaticTenantSettingsResolver(AiTenantSettings.CreateDefault(tenantId)));

        var clause = await resolver.ResolveClauseAsync(tenantId);

        clause.Should().BeNull();
    }

    [Fact]
    public async Task BrandPromptResolver_Includes_Name_Tone_And_Instructions()
    {
        var tenantId = Guid.NewGuid();
        var settings = AiTenantSettings.CreateDefault(tenantId);
        settings.UpdateBrandProfile(
            assistantDisplayName: "Forma",
            tone: "Calm and practical",
            avatarFileId: null,
            brandInstructions: "Use concise language.");
        var resolver = new AiBrandPromptResolver(new StaticTenantSettingsResolver(settings));

        var clause = await resolver.ResolveClauseAsync(tenantId);

        clause.Should().NotBeNull();
        clause.Should().Contain("Tenant AI brand profile:");
        clause.Should().Contain("- Name: Forma");
        clause.Should().Contain("- Tone: Calm and practical");
        clause.Should().Contain("- Brand guidance: Use concise language.");
    }

    private sealed class StaticTenantSettingsResolver(AiTenantSettings settings) : IAiTenantSettingsResolver
    {
        public Task<AiTenantSettings> GetOrDefaultAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(settings);

        public Task<ProviderCredentialPolicy> ResolveEffectivePolicyAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(settings.RequestedProviderCredentialPolicy);
    }
}
