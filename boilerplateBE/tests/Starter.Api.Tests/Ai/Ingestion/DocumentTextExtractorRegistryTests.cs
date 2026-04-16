using FluentAssertions;
using Moq;
using Starter.Module.AI.Application.Services.Ingestion;
using Starter.Module.AI.Infrastructure.Ingestion;
using Xunit;

namespace Starter.Api.Tests.Ai.Ingestion;

public sealed class DocumentTextExtractorRegistryTests
{
    [Fact]
    public void Resolve_Returns_Registered_Extractor_By_ContentType()
    {
        var mock = new Mock<IDocumentTextExtractor>();
        mock.SetupGet(e => e.SupportedContentTypes).Returns(new[] { "text/plain" });

        var registry = new DocumentTextExtractorRegistry(new[] { mock.Object });

        registry.Resolve("text/plain").Should().Be(mock.Object);
    }

    [Fact]
    public void Resolve_Is_Case_Insensitive_And_Ignores_Parameters()
    {
        var mock = new Mock<IDocumentTextExtractor>();
        mock.SetupGet(e => e.SupportedContentTypes).Returns(new[] { "text/plain" });

        var registry = new DocumentTextExtractorRegistry(new[] { mock.Object });

        registry.Resolve("TEXT/PLAIN; charset=utf-8").Should().Be(mock.Object);
    }

    [Fact]
    public void Resolve_Returns_Null_For_Unknown_Type()
    {
        var registry = new DocumentTextExtractorRegistry(Array.Empty<IDocumentTextExtractor>());
        registry.Resolve("application/x-zip").Should().BeNull();
    }
}
