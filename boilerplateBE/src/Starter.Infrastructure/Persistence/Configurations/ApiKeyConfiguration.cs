using Starter.Domain.ApiKeys.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(k => k.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(k => k.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(k => k.KeyPrefix)
            .HasColumnName("key_prefix")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(k => k.KeyPrefix)
            .IsUnique();

        builder.HasIndex(k => k.TenantId);

        builder.Property(k => k.KeyHash)
            .HasColumnName("key_hash")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(k => k.Scopes)
            .HasColumnName("scopes")
            .HasColumnType("text[]");

        builder.Property(k => k.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(k => k.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(k => k.IsRevoked)
            .HasColumnName("is_revoked")
            .IsRequired();

        builder.Property(k => k.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(k => k.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(k => k.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(k => k.ModifiedBy)
            .HasColumnName("modified_by");
    }
}
