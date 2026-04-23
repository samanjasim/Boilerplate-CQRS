using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Reranking;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public class RerankStrategySelectorTests
{
    private static RerankStrategySelector Selector(RerankStrategy cfg) =>
        new(new AiRagSettings { RerankStrategy = cfg });

    [Fact]
    public void Override_takes_precedence_over_settings()
    {
        var s = Selector(RerankStrategy.Listwise);
        var ctx = new RerankContext(null, RerankStrategy.Off);

        s.Resolve(ctx).Should().Be(RerankStrategy.Off);
    }

    [Fact]
    public void Off_setting_returns_off()
    {
        Selector(RerankStrategy.Off).Resolve(new RerankContext(QuestionType.Reasoning, null))
            .Should().Be(RerankStrategy.Off);
    }

    [Fact]
    public void Listwise_setting_returns_listwise()
    {
        Selector(RerankStrategy.Listwise).Resolve(new RerankContext(QuestionType.Reasoning, null))
            .Should().Be(RerankStrategy.Listwise);
    }

    [Fact]
    public void Pointwise_setting_returns_pointwise()
    {
        Selector(RerankStrategy.Pointwise).Resolve(new RerankContext(QuestionType.Greeting, null))
            .Should().Be(RerankStrategy.Pointwise);
    }

    [Theory]
    [InlineData(QuestionType.Greeting, RerankStrategy.Off)]
    [InlineData(QuestionType.Reasoning, RerankStrategy.Pointwise)]
    [InlineData(QuestionType.Definition, RerankStrategy.Listwise)]
    [InlineData(QuestionType.Listing, RerankStrategy.Listwise)]
    [InlineData(QuestionType.Other, RerankStrategy.Listwise)]
    public void Auto_routes_on_question_type(QuestionType qt, RerankStrategy expected)
    {
        Selector(RerankStrategy.Auto).Resolve(new RerankContext(qt, null))
            .Should().Be(expected);
    }

    [Fact]
    public void Auto_with_null_question_type_defaults_to_listwise()
    {
        Selector(RerankStrategy.Auto).Resolve(new RerankContext(null, null))
            .Should().Be(RerankStrategy.Listwise);
    }
}
