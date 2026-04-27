using System.Globalization;
using System.Resources;
using FluentAssertions;
using Xunit;

namespace Starter.Api.Tests.Ai.Moderation;

public sealed class ModerationRefusalResourceTests
{
    [Theory]
    [InlineData("Standard.Internal", "en")]
    [InlineData("Standard.EndCustomer", "en")]
    [InlineData("Standard.Anonymous", "en")]
    [InlineData("Standard.ProviderUnavailable", "en")]
    [InlineData("ChildSafe.Internal", "en")]
    [InlineData("ChildSafe.EndCustomer", "en")]
    [InlineData("ChildSafe.Anonymous", "en")]
    [InlineData("ChildSafe.ProviderUnavailable", "en")]
    [InlineData("ProfessionalModerated.Internal", "en")]
    [InlineData("ProfessionalModerated.EndCustomer", "en")]
    [InlineData("ProfessionalModerated.Anonymous", "en")]
    [InlineData("ProfessionalModerated.ProviderUnavailable", "en")]
    [InlineData("Standard.Internal", "ar")]
    [InlineData("Standard.EndCustomer", "ar")]
    [InlineData("Standard.Anonymous", "ar")]
    [InlineData("Standard.ProviderUnavailable", "ar")]
    [InlineData("ChildSafe.Internal", "ar")]
    [InlineData("ChildSafe.EndCustomer", "ar")]
    [InlineData("ChildSafe.Anonymous", "ar")]
    [InlineData("ChildSafe.ProviderUnavailable", "ar")]
    [InlineData("ProfessionalModerated.Internal", "ar")]
    [InlineData("ProfessionalModerated.EndCustomer", "ar")]
    [InlineData("ProfessionalModerated.Anonymous", "ar")]
    [InlineData("ProfessionalModerated.ProviderUnavailable", "ar")]
    public void All_Required_Keys_Resolve(string key, string culture)
    {
        var rm = new ResourceManager(
            "Starter.Module.AI.Resources.ModerationRefusalTemplates",
            typeof(Starter.Module.AI.AIModule).Assembly);
        var value = rm.GetString(key, new CultureInfo(culture));
        value.Should().NotBeNullOrWhiteSpace();
    }
}
