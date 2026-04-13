using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.CommentsActivity.Domain.Entities;

namespace Starter.Module.CommentsActivity.Infrastructure.Configurations;

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(c => c.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(c => c.ParentCommentId)
            .HasColumnName("parent_comment_id");

        builder.Property(c => c.AuthorId)
            .HasColumnName("author_id")
            .IsRequired();

        builder.Property(c => c.Body)
            .HasColumnName("body")
            .HasMaxLength(10000)
            .IsRequired();

        builder.Property(c => c.MentionsJson)
            .HasColumnName("mentions_json")
            .HasMaxLength(2000);

        builder.Property(c => c.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(c => c.DeletedBy)
            .HasColumnName("deleted_by");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(c => c.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(c => c.ModifiedBy)
            .HasColumnName("modified_by");

        // Self-referencing relationship: replies
        builder.HasMany(c => c.Replies)
            .WithOne(c => c.ParentComment)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Attachments
        builder.HasMany(c => c.Attachments)
            .WithOne()
            .HasForeignKey(a => a.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Reactions
        builder.HasMany(c => c.Reactions)
            .WithOne()
            .HasForeignKey(r => r.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.EntityType, c.EntityId });
        builder.HasIndex(c => c.ParentCommentId);
        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => c.AuthorId);
    }
}
