using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class FileMetadataConfiguration : IEntityTypeConfiguration<FileMetadata>
{
    public void Configure(EntityTypeBuilder<FileMetadata> builder)
    {
        builder.ToTable("files");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(f => f.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(f => f.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasIndex(f => f.StorageKey)
            .IsUnique();

        builder.Property(f => f.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.Size)
            .HasColumnName("size")
            .IsRequired();

        builder.Property(f => f.Category)
            .HasColumnName("category")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(f => f.Tags)
            .HasColumnName("tags")
            .HasMaxLength(2000);

        builder.Property(f => f.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(f => f.UploadedBy)
            .HasColumnName("uploaded_by")
            .IsRequired();

        builder.Property(f => f.Visibility)
            .HasColumnName("visibility")
            .HasConversion<int>()
            .HasDefaultValue(Starter.Domain.Common.Access.Enums.ResourceVisibility.Private)
            .IsRequired();

        builder.Property(f => f.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(f => f.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100);

        builder.Property(f => f.EntityId)
            .HasColumnName("entity_id");

        builder.Property(f => f.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(f => f.Origin)
            .HasColumnName("origin")
            .IsRequired();

        builder.Property(f => f.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(f => f.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(f => f.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(f => f.ModifiedBy)
            .HasColumnName("modified_by");

        builder.HasIndex(f => f.TenantId);
        builder.HasIndex(f => f.Category);
        builder.HasIndex(f => new { f.EntityType, f.EntityId });
        builder.HasIndex(f => f.UploadedBy);
        builder.HasIndex(f => f.CreatedAt);
        builder.HasIndex(f => new { f.Status, f.ExpiresAt });
        builder.HasIndex(f => f.Origin);
    }
}
