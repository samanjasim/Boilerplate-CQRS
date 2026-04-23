namespace Starter.Module.AI.Application.Services.Ingestion;

public interface IDocumentTextExtractorRegistry
{
    IDocumentTextExtractor? Resolve(string contentType);
}
