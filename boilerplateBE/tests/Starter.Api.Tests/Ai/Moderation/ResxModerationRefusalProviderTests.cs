using System.Globalization;
using FluentAssertions;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Services.Moderation;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class ResxModerationRefusalProviderTests
{
    [Theory]
    [InlineData(SafetyPreset.ChildSafe, PersonaAudienceType.Internal, "en")]
    [InlineData(SafetyPreset.ChildSafe, PersonaAudienceType.Internal, "ar")]
    [InlineData(SafetyPreset.ProfessionalModerated, PersonaAudienceType.EndCustomer, "en")]
    [InlineData(SafetyPreset.Standard, PersonaAudienceType.Anonymous, "en")]
    [InlineData(SafetyPreset.Standard, PersonaAudienceType.Anonymous, "ar")]
    public void Returns_Non_Empty_Refusal(SafetyPreset preset, PersonaAudienceType audience, string culture)
    {
        var p = new ResxModerationRefusalProvider();
        p.GetRefusal(preset, audience, new CultureInfo(culture)).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Provider_Unavailable_Resolves_For_Each_Preset()
    {
        var p = new ResxModerationRefusalProvider();
        foreach (var preset in Enum.GetValues<SafetyPreset>())
            p.GetProviderUnavailable(preset, CultureInfo.InvariantCulture).Should().NotBeNullOrWhiteSpace();
    }
}
