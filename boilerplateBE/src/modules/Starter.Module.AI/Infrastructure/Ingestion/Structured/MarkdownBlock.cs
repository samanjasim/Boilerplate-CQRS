using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal enum BlockType { Body, Heading, Code, Math, Table, List, Quote }

internal sealed record MarkdownBlock(
    BlockType Type,
    string Text,
    int HeadingLevel = 0,
    string? CodeLanguage = null)
{
    public ChunkType ToChunkType() => Type switch
    {
        BlockType.Heading => ChunkType.Heading,
        BlockType.Code => ChunkType.Code,
        BlockType.Math => ChunkType.Math,
        BlockType.Table => ChunkType.Table,
        BlockType.List => ChunkType.List,
        BlockType.Quote => ChunkType.Quote,
        _ => ChunkType.Body,
    };
}
