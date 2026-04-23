using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.CommentsActivity.Domain.Entities;

namespace Starter.Module.CommentsActivity.Infrastructure.Configurations;

internal sealed class CommentAttachmentConfiguration : IEntityTypeConfiguration<CommentAttachment>
{
    public void Configure(EntityTypeBuilder<CommentAttachment> builder)
    {
        builder.ToTable("comment_attachments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(a => a.CommentId)
            .HasColumnName("comment_id")
            .IsRequired();

        builder.Property(a => a.FileMetadataId)
            .HasColumnName("file_metadata_id")
            .IsRequired();

        builder.Property(a => a.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.ModifiedAt)
            .HasColumnName("modified_at");

        builder.HasIndex(a => a.CommentId);
    }
}
