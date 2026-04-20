namespace Starter.Module.AI.Application.Services.Ingestion;

public interface IDocumentChunker
{
    HierarchicalChunks Chunk(ExtractedDocument document, ChunkingOptions options);
}

public sealed record ChunkingOptions(int ParentTokens, int ChildTokens, int ChildOverlapTokens)
{
    public string? ContentType { get; init; }
}
