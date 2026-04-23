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

    [Fact]
    public void Fenced_code_block_with_language_is_atomic()
    {
        var md = "```python\nx = 1\nprint(x)\n```\n";
        var blocks = Tokenize(md);
        blocks.Should().ContainSingle().Which.Should().Match<MarkdownBlock>(b =>
            b.Type == BlockType.Code && b.CodeLanguage == "python" && b.Text == "x = 1\nprint(x)");
    }

    [Fact]
    public void Fenced_code_without_language_is_still_code()
    {
        var blocks = Tokenize("```\nhello\n```\n");
        blocks.Single().Type.Should().Be(BlockType.Code);
        blocks.Single().CodeLanguage.Should().BeNull();
    }

    [Fact]
    public void Body_before_code_fence_is_its_own_block()
    {
        var md = "intro\n\n```\ncode\n```\n";
        var blocks = Tokenize(md);
        blocks.Should().HaveCount(2);
        blocks[0].Type.Should().Be(BlockType.Body);
        blocks[1].Type.Should().Be(BlockType.Code);
    }

    [Fact]
    public void Unterminated_code_fence_is_treated_as_code_to_end_of_input()
    {
        var md = "```\nleft open\n";
        var blocks = Tokenize(md);
        blocks.Should().ContainSingle().Which.Type.Should().Be(BlockType.Code);
    }

    [Fact]
    public void Dollar_dollar_math_block_is_atomic()
    {
        var md = "$$\na = b + c\n$$\n";
        var blocks = Tokenize(md);
        blocks.Should().ContainSingle().Which.Type.Should().Be(BlockType.Math);
        blocks.Single().Text.Should().Be("a = b + c");
    }

    [Fact]
    public void Begin_equation_math_block_is_atomic()
    {
        var md = "\\begin{equation}\nE = mc^2\n\\end{equation}\n";
        var blocks = Tokenize(md);
        blocks.Single().Type.Should().Be(BlockType.Math);
    }

    [Fact]
    public void Adjacent_math_blocks_separated_only_by_whitespace_merge()
    {
        var md = "$$\na = 1\n$$\n\n$$\nb = 2\n$$\n";
        var blocks = Tokenize(md);
        blocks.Should().ContainSingle().Which.Type.Should().Be(BlockType.Math);
        blocks.Single().Text.Should().Contain("a = 1").And.Contain("b = 2");
    }

    [Fact]
    public void Pipe_table_with_separator_row_is_atomic()
    {
        var md = "| a | b |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |\n";
        var blocks = Tokenize(md);
        blocks.Should().ContainSingle().Which.Type.Should().Be(BlockType.Table);
        blocks.Single().Text.Should().Contain("1 | 2").And.Contain("3 | 4");
    }

    [Fact]
    public void Pipe_rows_without_separator_are_body_not_table()
    {
        var md = "| stray | line |\nfollowed by prose\n";
        var blocks = Tokenize(md);
        blocks.Single().Type.Should().Be(BlockType.Body);
    }

    [Fact]
    public void Hyphen_list_collapses_to_list_block()
    {
        var md = "- one\n- two\n- three\n";
        var blocks = Tokenize(md);
        blocks.Single().Type.Should().Be(BlockType.List);
        blocks.Single().Text.Should().Contain("- one").And.Contain("- three");
    }

    [Fact]
    public void Numbered_list_also_recognized()
    {
        var md = "1. first\n2. second\n";
        Tokenize(md).Single().Type.Should().Be(BlockType.List);
    }
}
