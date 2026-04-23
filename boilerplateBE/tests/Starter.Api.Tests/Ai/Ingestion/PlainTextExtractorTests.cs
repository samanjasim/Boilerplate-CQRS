using System.Text;
using FluentAssertions;
using Starter.Module.AI.Infrastructure.Ingestion.Extractors;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion;

public sealed class PlainTextExtractorTests
{
    [Fact]
    public async Task Extracts_Entire_Stream_As_Single_Page()
    {
        var extractor = new PlainTextExtractor();
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("line one\nline two"));

        var result = await extractor.ExtractAsync(ms, CancellationToken.None);

        result.Pages.Should().HaveCount(1);
        result.Pages[0].PageNumber.Should().Be(1);
        result.Pages[0].Text.Should().Be("line one\nline two");
        result.UsedOcr.Should().BeFalse();
    }

    [Fact]
    public void Advertises_TextPlain_And_Markdown()
    {
        new PlainTextExtractor().SupportedContentTypes
            .Should().BeEquivalentTo(new[] { "text/plain", "text/markdown" });
    }
}
