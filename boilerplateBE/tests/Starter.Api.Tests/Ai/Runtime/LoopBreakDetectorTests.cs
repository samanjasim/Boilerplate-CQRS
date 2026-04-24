using FluentAssertions;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Infrastructure.Providers;
using Xunit;

namespace Starter.Api.Tests.Ai.Runtime;

public sealed class LoopBreakDetectorTests
{
    private static AiToolCall Call(string name, string args) =>
        new(Id: Guid.NewGuid().ToString(), Name: name, ArgumentsJson: args);

    [Fact]
    public void Two_Identical_Calls_Do_Not_Trip_Break()
    {
        var detector = new LoopBreakDetector(LoopBreakPolicy.Default);
        detector.ShouldBreak(Call("search", """{"q":"x"}""")).Should().BeFalse();
        detector.ShouldBreak(Call("search", """{"q":"x"}""")).Should().BeFalse();
    }

    [Fact]
    public void Three_Identical_Calls_Trip_Break()
    {
        var detector = new LoopBreakDetector(LoopBreakPolicy.Default);
        detector.ShouldBreak(Call("search", """{"q":"x"}""")).Should().BeFalse();
        detector.ShouldBreak(Call("search", """{"q":"x"}""")).Should().BeFalse();
        detector.ShouldBreak(Call("search", """{"q":"x"}""")).Should().BeTrue();
    }

    [Fact]
    public void Three_Identical_With_Reordered_Json_Args_Still_Trip_Break()
    {
        var detector = new LoopBreakDetector(LoopBreakPolicy.Default);
        detector.ShouldBreak(Call("search", """{"q":"x","page":1}""")).Should().BeFalse();
        detector.ShouldBreak(Call("search", """{"page":1,"q":"x"}""")).Should().BeFalse();
        detector.ShouldBreak(Call("search", """{"q":"x","page":1}""")).Should().BeTrue();
    }

    [Fact]
    public void Non_Identical_Calls_Do_Not_Trip_Break()
    {
        var detector = new LoopBreakDetector(LoopBreakPolicy.Default);
        detector.ShouldBreak(Call("search", """{"q":"a"}""")).Should().BeFalse();
        detector.ShouldBreak(Call("search", """{"q":"b"}""")).Should().BeFalse();
        detector.ShouldBreak(Call("search", """{"q":"a"}""")).Should().BeFalse();
    }

    [Fact]
    public void Different_Tool_Names_Do_Not_Trip_Break()
    {
        var detector = new LoopBreakDetector(LoopBreakPolicy.Default);
        detector.ShouldBreak(Call("a", "{}")).Should().BeFalse();
        detector.ShouldBreak(Call("b", "{}")).Should().BeFalse();
        detector.ShouldBreak(Call("a", "{}")).Should().BeFalse();
    }

    [Fact]
    public void Disabled_Policy_Never_Trips_Break()
    {
        var detector = new LoopBreakDetector(new LoopBreakPolicy(Enabled: false));
        detector.ShouldBreak(Call("x", "{}")).Should().BeFalse();
        detector.ShouldBreak(Call("x", "{}")).Should().BeFalse();
        detector.ShouldBreak(Call("x", "{}")).Should().BeFalse();
        detector.ShouldBreak(Call("x", "{}")).Should().BeFalse();
    }

    [Fact]
    public void Policy_With_Different_MaxRepeats_Is_Respected()
    {
        var detector = new LoopBreakDetector(new LoopBreakPolicy(Enabled: true, MaxIdenticalRepeats: 2));
        detector.ShouldBreak(Call("x", "{}")).Should().BeFalse();
        detector.ShouldBreak(Call("x", "{}")).Should().BeTrue();
    }
}
