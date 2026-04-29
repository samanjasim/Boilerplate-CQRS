using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Persistence.Seed;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

/// <summary>
/// Guards against the C2-fix regression: seeded threshold + always-block category
/// keys MUST match the canonical OpenAI Moderation API wire-format strings, because
/// OpenAiContentModerator.ProjectScores produces the dictionary keyed by those
/// strings. A mismatch silently bypasses always-block and threshold checks.
///
/// This test enumerates the canonical OpenAI moderation category keys (the keys
/// that ProjectScores can produce) and asserts every key appearing in
/// SafetyPresetProfileSeed.SeedAsync's three platform-default rows is in that set.
/// </summary>
public sealed class SeedCategoriesAlignedWithOpenAiWireKeysTests
{
    // Canonical keys produced by OpenAiContentModerator.ProjectScores. If you add a
    // category here, also update ProjectScores. If you add a property to the
    // OpenAI SDK's ModerationResult, also update both sides.
    private static readonly HashSet<string> CanonicalKeys = new(StringComparer.Ordinal)
    {
        "harassment", "harassment/threatening",
        "hate", "hate/threatening",
        "illicit", "illicit/violent",
        "self-harm", "self-harm/instructions", "self-harm/intent",
        "sexual", "sexual/minors",
        "violence", "violence/graphic"
    };

    [Theory]
    [InlineData("""{"sexual":0.85,"hate":0.85,"violence":0.85,"self-harm":0.85,"harassment":0.85}""")]
    [InlineData("""{"sexual":0.5,"hate":0.5,"violence":0.5,"self-harm":0.3,"harassment":0.5}""")]
    public void Threshold_Keys_Are_Canonical(string thresholdsJson)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, double>>(thresholdsJson)!;
        foreach (var key in dict.Keys)
            CanonicalKeys.Should().Contain(key, $"threshold key '{key}' must match an OpenAI wire-format category");
    }

    [Theory]
    [InlineData("""["sexual/minors","violence/graphic"]""")]
    public void BlockedCategory_Keys_Are_Canonical(string blockedJson)
    {
        var keys = JsonSerializer.Deserialize<List<string>>(blockedJson)!;
        foreach (var key in keys)
            CanonicalKeys.Should().Contain(key, $"always-block key '{key}' must match an OpenAI wire-format category");
    }

    [Fact]
    public async Task Seeded_Profiles_Use_Canonical_Keys()
    {
        var cu = new Mock<ICurrentUserService>();
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"seed-keys-{Guid.NewGuid()}").Options;
        using var db = new AiDbContext(opts, cu.Object);
        await SafetyPresetProfileSeed.SeedAsync(db, default);

        var profiles = await db.AiSafetyPresetProfiles.IgnoreQueryFilters().ToListAsync();
        profiles.Should().HaveCount(3);

        foreach (var profile in profiles)
        {
            var thresholds = JsonSerializer.Deserialize<Dictionary<string, double>>(profile.CategoryThresholdsJson)!;
            foreach (var key in thresholds.Keys)
                CanonicalKeys.Should().Contain(key,
                    $"threshold key '{key}' on preset '{profile.Preset}' must match an OpenAI wire-format category");

            var blocked = JsonSerializer.Deserialize<List<string>>(profile.BlockedCategoriesJson)!;
            foreach (var key in blocked)
                CanonicalKeys.Should().Contain(key,
                    $"always-block key '{key}' on preset '{profile.Preset}' must match an OpenAI wire-format category");
        }
    }
}
