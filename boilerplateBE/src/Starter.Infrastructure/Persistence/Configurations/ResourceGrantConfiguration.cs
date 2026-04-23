using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Common.Access;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class ResourceGrantConfiguration : IEntityTypeConfiguration<ResourceGrant>
{
    public void Configure(EntityTypeBuilder<ResourceGrant> builder)
    {
        builder.ToTable("resource_grants");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(g => g.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(g => g.ResourceType)
            .HasColumnName("resource_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(g => g.ResourceId)
            .HasColumnName("resource_id")
            .IsRequired();

        builder.Property(g => g.SubjectType)
            .HasColumnName("subject_type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(g => g.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.Property(g => g.Level)
            .HasColumnName("level")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(g => g.GrantedByUserId)
            .HasColumnName("granted_by_user_id")
            .IsRequired();

        builder.Property(g => g.GrantedAt)
            .HasColumnName("granted_at")
            .IsRequired();

        builder.Property(g => g.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(g => g.ModifiedAt)
            .HasColumnName("modified_at");

        builder.HasIndex(g => new { g.TenantId, g.ResourceType, g.ResourceId, g.SubjectType, g.SubjectId })
            .IsUnique()
            .HasDatabaseName("ix_resource_grants_unique");

        builder.HasIndex(g => new { g.TenantId, g.ResourceType, g.ResourceId })
            .HasDatabaseName("ix_resource_grants_by_resource");

        builder.HasIndex(g => new { g.TenantId, g.SubjectType, g.SubjectId, g.ResourceType })
            .HasDatabaseName("ix_resource_grants_by_subject");
    }
}
