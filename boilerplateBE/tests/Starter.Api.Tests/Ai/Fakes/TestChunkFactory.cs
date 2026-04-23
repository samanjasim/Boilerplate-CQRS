using System.Reflection;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Api.Tests.Ai.Fakes;

/// <summary>
/// AiDocumentChunk has private setters (aggregate root); tests use reflection to
/// construct instances with controlled values. Only the fields reranker/rewriter
/// tests care about are settable here — extend as new tests need more fields.
/// </summary>
internal static class TestChunkFactory
{
    public static AiDocumentChunk Build(
        Guid? pointId = null,
        Guid? documentId = null,
        int chunkIndex = 0,
        string? content = null,
        string chunkLevel = "child",
        int? pageNumber = null,
        string? sectionTitle = null,
        Guid? parentChunkId = null,
        int tokenCount = 0)
    {
        var chunk = (AiDocumentChunk)Activator.CreateInstance(typeof(AiDocumentChunk), nonPublic: true)!;
        SetProp(chunk, "Id", Guid.NewGuid());
        SetProp(chunk, nameof(AiDocumentChunk.QdrantPointId), pointId ?? Guid.NewGuid());
        SetProp(chunk, nameof(AiDocumentChunk.DocumentId), documentId ?? Guid.NewGuid());
        SetProp(chunk, nameof(AiDocumentChunk.ChunkIndex), chunkIndex);
        SetProp(chunk, nameof(AiDocumentChunk.Content), content ?? $"content-{chunkIndex}");
        SetProp(chunk, nameof(AiDocumentChunk.ChunkLevel), chunkLevel);
        SetProp(chunk, nameof(AiDocumentChunk.PageNumber), pageNumber);
        SetProp(chunk, nameof(AiDocumentChunk.SectionTitle), sectionTitle);
        SetProp(chunk, nameof(AiDocumentChunk.ParentChunkId), parentChunkId);
        SetProp(chunk, nameof(AiDocumentChunk.TokenCount), tokenCount);
        return chunk;
    }

    private static void SetProp(object target, string name, object? value)
    {
        // Walk the type hierarchy to find the property (Id lives on BaseEntity<Guid>)
        var type = target.GetType();
        PropertyInfo? p = null;
        while (type is not null && p is null)
        {
            p = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            type = type.BaseType;
        }

        if (p is null)
            throw new InvalidOperationException($"Property {name} not found on {target.GetType()} or its base types");

        var setter = p.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException($"Property {name} has no setter");

        setter.Invoke(target, new[] { value });
    }
}
