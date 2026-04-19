using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class RuleBasedQueryRewriterTests
{
    [Fact]
    public void EnglishQuestionWord_IsStripped()
    {
        var result = RuleBasedQueryRewriter.Rewrite("what is photosynthesis?");
        result.Should().Contain("photosynthesis");
    }

    [Fact]
    public void ArabicQuestionWord_IsStripped()
    {
        var result = RuleBasedQueryRewriter.Rewrite("ما هو التمثيل الضوئي؟");
        result.Should().Contain("التمثيل الضوئي");
    }

    [Fact]
    public void TrailingPoliteTokens_AreStripped_English()
    {
        var result = RuleBasedQueryRewriter.Rewrite("tell me about oxygen please");
        result.Should().Contain("tell me about oxygen");
    }

    [Fact]
    public void TrailingPoliteTokens_AreStripped_Arabic()
    {
        var result = RuleBasedQueryRewriter.Rewrite("اذكر عناصر الهواء من فضلك");
        result.Should().Contain("اذكر عناصر الهواء");
    }

    [Fact]
    public void WhenVariantEqualsOriginal_NotDuplicated()
    {
        var result = RuleBasedQueryRewriter.Rewrite("photosynthesis");
        result.Should().HaveCount(1);
        result[0].Should().Be("photosynthesis");
    }

    [Fact]
    public void WhitespaceCollapsed()
    {
        var result = RuleBasedQueryRewriter.Rewrite("what   is    photosynthesis");
        result.Should().NotContain(s => s.Contains("  "));
    }

    [Fact]
    public void EmptyInput_ReturnsEmptyList()
    {
        RuleBasedQueryRewriter.Rewrite("").Should().BeEmpty();
        RuleBasedQueryRewriter.Rewrite("   ").Should().BeEmpty();
    }
}
