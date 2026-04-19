using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiDocumentChunkConfiguration : IEntityTypeConfiguration<AiDocumentChunk>
{
    public void Configure(EntityTypeBuilder<AiDocumentChunk> builder)
    {
        builder.ToTable("ai_document_chunks");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.DocumentId)
            .HasColumnName("document_id")
            .IsRequired();

        builder.Property(e => e.ParentChunkId)
            .HasColumnName("parent_chunk_id");

        builder.Property(e => e.ChunkLevel)
            .HasColumnName("chunk_level")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(e => e.ChunkIndex)
            .HasColumnName("chunk_index")
            .IsRequired();

        builder.Property(e => e.SectionTitle)
            .HasColumnName("section_title")
            .HasMaxLength(500);

        builder.Property(e => e.PageNumber)
            .HasColumnName("page_number");

        builder.Property(e => e.TokenCount)
            .HasColumnName("token_count")
            .IsRequired();

        builder.Property(e => e.QdrantPointId)
            .HasColumnName("qdrant_point_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(e => e.NormalizedContent)
            .HasColumnName("normalized_content");

        builder.HasIndex(e => e.DocumentId);
        builder.HasIndex(e => e.ParentChunkId);
        builder.HasIndex(e => e.QdrantPointId)
            .IsUnique();

        if (IsRelationalProvider(builder))
        {
            builder.Property<NpgsqlTsVector>("ContentTsVector")
                .HasColumnName("content_tsv")
                .HasComputedColumnSql(
                    "to_tsvector('simple', coalesce(normalized_content, content))",
                    stored: true);

            builder.HasIndex("ContentTsVector")
                .HasDatabaseName("ix_ai_document_chunks_content_tsv")
                .HasMethod("GIN");
        }
    }

    private static bool IsRelationalProvider(EntityTypeBuilder builder)
        => builder.Metadata.Model.FindAnnotation("Relational:MaxIdentifierLength") is not null;
}
