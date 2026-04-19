using FluentAssertions;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Infrastructure.Retrieval.Classification;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public class RegexQuestionClassifierTests
{
    [Theory]
    [InlineData("hi", QuestionType.Greeting)]
    [InlineData("hello there!", QuestionType.Greeting)]
    [InlineData("مرحبا", QuestionType.Greeting)]
    [InlineData("السلام عليكم", QuestionType.Greeting)]
    [InlineData("what is refund policy?", QuestionType.Definition)]
    [InlineData("ما هي سياسة الإرجاع", QuestionType.Definition)]
    [InlineData("list all products", QuestionType.Listing)]
    [InlineData("show me the customers", QuestionType.Listing)]
    [InlineData("اعرض لي العملاء", QuestionType.Listing)]
    [InlineData("why did the order fail?", QuestionType.Reasoning)]
    [InlineData("لماذا فشل الطلب", QuestionType.Reasoning)]
    public void Classifies_query(string input, QuestionType expected)
    {
        RegexQuestionClassifier.TryClassify(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("the forecast for Q3 is ambiguous based on these factors")]
    [InlineData("some unrelated narrative content")]
    public void Returns_null_when_no_pattern_matches(string input)
    {
        RegexQuestionClassifier.TryClassify(input).Should().BeNull();
    }
}
