using FluentAssertions;
using Starter.Module.AI.Infrastructure.Services.Personas;
using Xunit;

namespace Starter.Api.Tests.Ai.Personas;

public sealed class SlugGeneratorTests
{
    private static readonly SlugGenerator Gen = new();

    [Theory]
    [InlineData("Brand Content Agent", "brand-content-agent")]
    [InlineData("  TUTOR  ", "tutor")]
    [InlineData("hello / world", "hello-world")]
    [InlineData("Grade 5 Arabic Tutor", "grade-5-arabic-tutor")]
    [InlineData("a---b__c", "a-b-c")]
    [InlineData("", "untitled")]
    public void Slugify_Produces_Kebab_Case(string input, string expected)
    {
        Gen.Slugify(input).Should().Be(expected);
    }

    [Fact]
    public void EnsureUnique_Returns_Input_If_No_Collision()
    {
        var result = Gen.EnsureUnique("teacher", taken: new HashSet<string>());
        result.Should().Be("teacher");
    }

    [Fact]
    public void EnsureUnique_Appends_2_3_When_Collision()
    {
        var taken = new HashSet<string> { "teacher", "teacher-2" };
        Gen.EnsureUnique("teacher", taken).Should().Be("teacher-3");
    }

    [Fact]
    public void Slugify_Truncates_To_64_Chars()
    {
        var long_ = new string('a', 200);
        Gen.Slugify(long_).Length.Should().BeLessOrEqualTo(64);
    }
}
