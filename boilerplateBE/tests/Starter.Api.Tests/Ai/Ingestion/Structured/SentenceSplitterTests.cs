using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class SentenceSplitterTests
{
    [Fact]
    public void English_sentences_split_on_period_bang_question()
    {
        var parts = SentenceSplitter.Split("Hello world. This works! Does it?");
        parts.Should().HaveCount(3);
    }

    [Fact]
    public void Arabic_sentences_split_on_question_and_comma_markers()
    {
        var parts = SentenceSplitter.Split("ما هو الحل؟ الحل بسيط، لكنه فعّال.");
        parts.Should().HaveCountGreaterThan(1);
        parts[0].Should().Contain("ما هو الحل");
    }

    [Fact]
    public void Single_sentence_returns_one_part()
    {
        SentenceSplitter.Split("just one").Should().ContainSingle();
    }

    [Fact]
    public void Whitespace_only_input_returns_empty()
    {
        SentenceSplitter.Split("   \n").Should().BeEmpty();
    }
}
