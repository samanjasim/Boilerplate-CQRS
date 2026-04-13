using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiDocumentConfiguration : IEntityTypeConfiguration<AiDocument>
{
    public void Configure(EntityTypeBuilder<AiDocument> builder)
    {
        builder.ToTable("ai_documents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.FileRef)
            .HasColumnName("file_ref")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(e => e.ChunkCount)
            .HasColumnName("chunk_count")
            .IsRequired();

        builder.Property(e => e.EmbeddingStatus)
            .HasColumnName("embedding_status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.Property(e => e.RequiresOcr)
            .HasColumnName("requires_ocr")
            .IsRequired();

        builder.Property(e => e.OcrProvider)
            .HasColumnName("ocr_provider")
            .HasMaxLength(50);

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at");

        builder.Property(e => e.UploadedByUserId)
            .HasColumnName("uploaded_by_user_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.EmbeddingStatus);
    }
}
