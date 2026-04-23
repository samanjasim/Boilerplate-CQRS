using FluentAssertions;
using Starter.Module.AI.Infrastructure.Observability;
using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

[Collection(ObservabilityTestCollection.Name)]
public class RagLanguageDetectorTests
{
    [Theory]
    [InlineData("ما هي المضخة الطاردة المركزية؟", "ar")]
    [InlineData("How does a centrifugal pump work?", "en")]
    [InlineData("What is المضخة used for in engineering?", "mixed")]
    [InlineData("1234567890 !@# $$ ???", "unknown")]
    [InlineData("", "unknown")]
    [InlineData("   ", "unknown")]
    public void Detects_language_from_codepoint_ratio(string query, string expected)
    {
        RagLanguageDetector.Detect(query).Should().Be(expected);
    }
}
