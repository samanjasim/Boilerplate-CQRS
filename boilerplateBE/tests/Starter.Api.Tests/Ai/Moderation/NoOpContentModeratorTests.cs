using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.Services.Moderation;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class NoOpContentModeratorTests
{
    [Fact]
    public async Task Reports_Unavailable()
    {
        var moderator = new NoOpContentModerator();
        var profile = new ResolvedSafetyProfile(
            SafetyPreset.Standard, ModerationProvider.OpenAi,
            new Dictionary<string, double>(), Array.Empty<string>(),
            ModerationFailureMode.FailOpen, false);

        var verdict = await moderator.ScanAsync("hi", ModerationStage.Input, profile, null, default);

        verdict.ProviderUnavailable.Should().BeTrue();
    }
}
