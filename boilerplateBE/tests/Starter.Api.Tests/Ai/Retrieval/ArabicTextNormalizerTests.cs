using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class ArabicTextNormalizerTests
{
    private static readonly ArabicNormalizationOptions DefaultOpts = new(
        NormalizeTaMarbuta: true, NormalizeArabicDigits: true);

    [Fact]
    public void StripsDiacritics_KeepsBaseLetters()
    {
        var input = "مُؤَسَّسَة";   // with harakat
        var output = ArabicTextNormalizer.Normalize(input, DefaultOpts);
        output.Should().Be("موسسه");  // diacritics removed, hamza-on-waw → waw, ta-marbuta → ha
    }

    [Fact]
    public void NormalizesAlefVariants_ToBareAlef()
    {
        ArabicTextNormalizer.Normalize("أحمد", DefaultOpts).Should().Be("احمد");
        ArabicTextNormalizer.Normalize("إيمان", DefaultOpts).Should().Be("ايمان");
        ArabicTextNormalizer.Normalize("آمنة", DefaultOpts).Should().Be("امنه");
    }

    [Fact]
    public void NormalizesYa_FromAlefMaksura()
    {
        ArabicTextNormalizer.Normalize("على", DefaultOpts).Should().Be("علي");
    }

    [Fact]
    public void NormalizesTaMarbuta_ToHa_WhenEnabled()
    {
        ArabicTextNormalizer.Normalize("مدرسة", DefaultOpts).Should().Be("مدرسه");
    }

    [Fact]
    public void LeavesTaMarbuta_WhenDisabled()
    {
        var opts = DefaultOpts with { NormalizeTaMarbuta = false };
        ArabicTextNormalizer.Normalize("مدرسة", opts).Should().Be("مدرسة");
    }

    [Fact]
    public void StripsTatweel()
    {
        ArabicTextNormalizer.Normalize("ســـــلام", DefaultOpts).Should().Be("سلام");
    }

    [Fact]
    public void LeavesAsciiUnchanged()
    {
        ArabicTextNormalizer.Normalize("Hello World 2024", DefaultOpts).Should().Be("Hello World 2024");
    }

    [Fact]
    public void NormalizesMixedArabicEnglish_TouchesOnlyArabicRanges()
    {
        ArabicTextNormalizer.Normalize("مرحبا Hello مُدرسة", DefaultOpts).Should().Be("مرحبا Hello مدرسه");
    }

    [Fact]
    public void NormalizesArabicIndicDigits_WhenEnabled()
    {
        ArabicTextNormalizer.Normalize("سنة ٢٠٢٥", DefaultOpts).Should().Be("سنه 2025");
    }

    [Fact]
    public void LeavesArabicDigits_WhenDisabled()
    {
        var opts = DefaultOpts with { NormalizeArabicDigits = false };
        ArabicTextNormalizer.Normalize("٢٠٢٥", opts).Should().Be("٢٠٢٥");
    }

    [Fact]
    public void NormalizesHamzaOnYaAndWaw()
    {
        ArabicTextNormalizer.Normalize("سؤال", DefaultOpts).Should().Be("سوال");
        ArabicTextNormalizer.Normalize("شيء", DefaultOpts).Should().Be("شيء"); // hamza-on-bare stays (it's \u0621)
        ArabicTextNormalizer.Normalize("رئيس", DefaultOpts).Should().Be("رييس");
    }

    [Fact]
    public void CollapsesWhitespaceAndTrims()
    {
        ArabicTextNormalizer.Normalize("  مرحبا   بك  ", DefaultOpts).Should().Be("مرحبا بك");
    }

    [Fact]
    public void EmptyString_ReturnsEmpty()
    {
        ArabicTextNormalizer.Normalize("", DefaultOpts).Should().Be("");
    }

    [Fact]
    public void NullInput_ReturnsEmpty()
    {
        ArabicTextNormalizer.Normalize(null!, DefaultOpts).Should().Be("");
    }
}
