using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiDocumentMappers
{
    public static AiDocumentDto ToDto(this AiDocument d) => new(
        Id: d.Id,
        Name: d.Name,
        FileName: d.FileName,
        ContentType: d.ContentType,
        SizeBytes: d.SizeBytes,
        ChunkCount: d.ChunkCount,
        EmbeddingStatus: d.EmbeddingStatus.ToString(),
        ErrorMessage: d.ErrorMessage,
        RequiresOcr: d.RequiresOcr,
        ProcessedAt: d.ProcessedAt,
        CreatedAt: d.CreatedAt,
        UploadedByUserId: d.UploadedByUserId);

    public static AiDocumentChunkPreviewDto ToPreviewDto(this AiDocumentChunk c, int previewChars = 160) => new(
        Id: c.Id,
        ChunkLevel: c.ChunkLevel,
        ChunkIndex: c.ChunkIndex,
        TokenCount: c.TokenCount,
        PageNumber: c.PageNumber,
        SectionTitle: c.SectionTitle,
        ContentPreview: c.Content.Length <= previewChars
            ? c.Content
            : c.Content[..previewChars] + "…");
}
