using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiProviderCredentialConfiguration : IEntityTypeConfiguration<AiProviderCredential>
{
    public void Configure(EntityTypeBuilder<AiProviderCredential> builder)
    {
        builder.ToTable("ai_provider_credentials");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.EncryptedSecret).HasColumnName("encrypted_secret").IsRequired();
        builder.Property(e => e.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(64).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(e => e.LastValidatedAt).HasColumnName("last_validated_at");
        builder.Property(e => e.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.ModifiedBy).HasColumnName("modified_by");

        builder.HasIndex(e => new { e.TenantId, e.Provider })
            .IsUnique()
            .HasFilter("status = 0")
            .HasDatabaseName("ux_ai_provider_credentials_active_tenant_provider");
    }
}
