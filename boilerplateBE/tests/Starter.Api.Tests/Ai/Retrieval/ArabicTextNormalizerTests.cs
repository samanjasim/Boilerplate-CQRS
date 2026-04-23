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
        ArabicTextNormalizer.Normalize("ٱلرحمن", DefaultOpts).Should().Be("الرحمن"); // U+0671 alef wasla
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
    public void StripsQuranicAnnotationMarks()
    {
        // U+06D6 (ARABIC SMALL HIGH LIGATURE SAD WITH LAM WITH ALEF MAKSURA) and
        // U+06E9 (ARABIC PLACE OF SAJDAH) — both in the annotation range.
        ArabicTextNormalizer.Normalize("مرحبا\u06D6\u06E9", DefaultOpts).Should().Be("مرحبا");
    }

    [Fact]
    public void StripsArabicExtendedACombiningMarks()
    {
        // U+08D3 is the first combining mark in Extended-A (ARABIC SMALL LOW WAW).
        // U+08E3 is a vowel sign (TURNED DAMMA BELOW).
        ArabicTextNormalizer.Normalize("سلام\u08D3\u08E3", DefaultOpts).Should().Be("سلام");
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
        ArabicTextNormalizer.Normalize("۲۰۲۵", DefaultOpts).Should().Be("2025"); // Extended Arabic-Indic (Persian/Urdu)
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
