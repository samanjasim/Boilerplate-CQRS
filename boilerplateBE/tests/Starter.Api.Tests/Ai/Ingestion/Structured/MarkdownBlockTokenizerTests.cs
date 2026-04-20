using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class MarkdownBlockTokenizerTests
{
    private static IReadOnlyList<MarkdownBlock> Tokenize(string input) =>
        new MarkdownBlockTokenizer().Tokenize(input);

    [Fact]
    public void Heading_emits_heading_block_with_level()
    {
        var blocks = Tokenize("# Chapter 1\n");
        blocks.Should().ContainSingle().Which
            .Should().Match<MarkdownBlock>(b => b.Type == BlockType.Heading && b.HeadingLevel == 1 && b.Text == "Chapter 1");
    }

    [Theory]
    [InlineData("## Section", 2)]
    [InlineData("###### Deep", 6)]
    public void Heading_levels_parsed(string line, int level)
    {
        Tokenize(line).Single().HeadingLevel.Should().Be(level);
    }

    [Fact]
    public void Paragraphs_separated_by_blank_lines_become_body_blocks()
    {
        var blocks = Tokenize("first para\n\nsecond para\n");
        blocks.Should().HaveCount(2);
        blocks[0].Type.Should().Be(BlockType.Body);
        blocks[0].Text.Should().Be("first para");
        blocks[1].Text.Should().Be("second para");
    }

    [Fact]
    public void Blockquote_lines_collapse_to_a_single_quote_block()
    {
        var blocks = Tokenize("> one\n> two\n\nafter\n");
        blocks.Should().HaveCount(2);
        blocks[0].Type.Should().Be(BlockType.Quote);
        blocks[0].Text.Should().Be("one\ntwo");
        blocks[1].Type.Should().Be(BlockType.Body);
    }

    [Fact]
    public void Arabic_heading_is_detected_same_as_english()
    {
        var blocks = Tokenize("# مقدمة\n");
        blocks.Should().ContainSingle().Which.Type.Should().Be(BlockType.Heading);
        blocks[0].Text.Should().Be("مقدمة");
    }
}
