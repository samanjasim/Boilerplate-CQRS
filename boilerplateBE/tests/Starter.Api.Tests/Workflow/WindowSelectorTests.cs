using FluentAssertions;
using Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class WindowSelectorTests
{
    [Theory]
    [InlineData("7d",  WindowSelector.SevenDays)]
    [InlineData("30d", WindowSelector.ThirtyDays)]
    [InlineData("90d", WindowSelector.NinetyDays)]
    [InlineData("all", WindowSelector.AllTime)]
    [InlineData("ALL", WindowSelector.AllTime)]
    [InlineData("30D", WindowSelector.ThirtyDays)]
    public void TryParse_ValidString_ReturnsExpectedEnum(string raw, WindowSelector expected)
    {
        var ok = WindowSelectorParser.TryParse(raw, out var value);

        ok.Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1d")]
    [InlineData("180d")]
    [InlineData("custom")]
    public void TryParse_Invalid_ReturnsFalse(string? raw)
    {
        var ok = WindowSelectorParser.TryParse(raw, out _);
        ok.Should().BeFalse();
    }
}
