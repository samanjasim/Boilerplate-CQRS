using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion.Structured;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion.Structured;

public class HeadingStackTests
{
    [Fact]
    public void Empty_stack_has_empty_breadcrumb()
    {
        new HeadingStack().Breadcrumb.Should().BeEmpty();
    }

    [Fact]
    public void Push_sets_breadcrumb_to_single_entry()
    {
        var s = new HeadingStack();
        s.Push(1, "Chapter 1");
        s.Breadcrumb.Should().Be("Chapter 1");
    }

    [Fact]
    public void Pushing_deeper_level_appends()
    {
        var s = new HeadingStack();
        s.Push(1, "Chapter 1");
        s.Push(2, "Section 1.2");
        s.Breadcrumb.Should().Be("Chapter 1 > Section 1.2");
    }

    [Fact]
    public void Pushing_same_level_replaces_tail()
    {
        var s = new HeadingStack();
        s.Push(1, "Chapter 1");
        s.Push(2, "Section 1.1");
        s.Push(2, "Section 1.2");
        s.Breadcrumb.Should().Be("Chapter 1 > Section 1.2");
    }

    [Fact]
    public void Pushing_shallower_level_pops_and_replaces()
    {
        var s = new HeadingStack();
        s.Push(1, "Chapter 1");
        s.Push(2, "Section 1.1");
        s.Push(3, "Sub 1.1.1");
        s.Push(2, "Section 1.2");
        s.Breadcrumb.Should().Be("Chapter 1 > Section 1.2");
    }

    [Fact]
    public void Reset_clears_everything()
    {
        var s = new HeadingStack();
        s.Push(1, "A");
        s.Reset();
        s.Breadcrumb.Should().BeEmpty();
    }

    [Fact]
    public void Section_title_returns_deepest_heading()
    {
        var s = new HeadingStack();
        s.Push(1, "Ch1");
        s.Push(2, "Sec1");
        s.DeepestHeading.Should().Be("Sec1");
    }

    [Fact]
    public void Arabic_headings_are_supported_unchanged()
    {
        var s = new HeadingStack();
        s.Push(1, "مقدمة");
        s.Push(2, "البداية");
        s.Breadcrumb.Should().Be("مقدمة > البداية");
    }
}
