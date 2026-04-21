using FluentAssertions;
using Starter.Module.AI.Infrastructure.Retrieval.QueryRewriting;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class ContextualFollowUpHeuristicTests
{
    [Theory]
    [InlineData("how do we configure it?")]
    [InlineData("and then?")]
    [InlineData("what about that?")]
    [InlineData("tell me more")]
    [InlineData("why?")]
    public void English_follow_up_phrases_trigger_heuristic(string msg)
        => ContextualFollowUpHeuristic.LooksLikeFollowUp(msg).Should().BeTrue();

    [Theory]
    [InlineData("كيف نضبطه؟")]
    [InlineData("وماذا عن هذا؟")]
    [InlineData("لماذا؟")]
    public void Arabic_follow_up_phrases_trigger_heuristic(string msg)
        => ContextualFollowUpHeuristic.LooksLikeFollowUp(msg).Should().BeTrue();

    [Theory]
    [InlineData("What is the default RRF constant used in hybrid fusion?")]
    [InlineData("ما هي مكونات نظام Qdrant الداخلية؟")]
    public void Self_contained_questions_do_not_trigger(string msg)
        => ContextualFollowUpHeuristic.LooksLikeFollowUp(msg).Should().BeFalse();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Whitespace_input_returns_false(string msg)
        => ContextualFollowUpHeuristic.LooksLikeFollowUp(msg).Should().BeFalse();

    [Fact]
    public void Short_messages_under_25_chars_trigger()
        => ContextualFollowUpHeuristic.LooksLikeFollowUp("more details please").Should().BeTrue();
}
