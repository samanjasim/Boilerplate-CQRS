using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class HtmlToMarkdownConverterTests
{
    private static HtmlToMarkdownConverter New() => new();

    [Fact]
    public void H1_becomes_hash_heading()
    {
        New().Convert("<h1>Chapter 1</h1>").Should().Contain("# Chapter 1");
    }

    [Fact]
    public void Paragraphs_are_preserved_with_blank_lines()
    {
        var md = New().Convert("<p>first</p><p>second</p>");
        md.Should().Contain("first").And.Contain("second");
    }

    [Fact]
    public void Inline_code_is_backticked()
    {
        New().Convert("Call <code>foo()</code>.").Should().Contain("`foo()`");
    }

    [Fact]
    public void Lists_become_markdown_lists()
    {
        var md = New().Convert("<ul><li>a</li><li>b</li></ul>");
        md.Should().MatchRegex(@"[-*]\s+a").And.MatchRegex(@"[-*]\s+b");
    }

    [Fact]
    public void Table_is_rendered_as_markdown_table()
    {
        var md = New().Convert("<table><tr><th>x</th><th>y</th></tr><tr><td>1</td><td>2</td></tr></table>");
        md.Should().Contain("| x | y |").And.Contain("| 1 | 2 |");
    }
}
