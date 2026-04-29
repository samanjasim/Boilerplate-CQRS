using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiWidgetCredentialConfiguration : IEntityTypeConfiguration<AiWidgetCredential>
{
    public void Configure(EntityTypeBuilder<AiWidgetCredential> builder)
    {
        builder.ToTable("ai_widget_credentials");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.WidgetId).HasColumnName("widget_id").IsRequired();
        builder.Property(e => e.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(64).IsRequired();
        builder.Property(e => e.KeyHash).HasColumnName("key_hash").HasMaxLength(500).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        builder.Property(e => e.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.ModifiedBy).HasColumnName("modified_by");

        builder.HasIndex(e => new { e.TenantId, e.WidgetId, e.Status })
            .HasDatabaseName("ix_ai_widget_credentials_tenant_widget_status");

        builder.HasIndex(e => e.KeyPrefix)
            .IsUnique()
            .HasDatabaseName("ux_ai_widget_credentials_key_prefix");

        builder.HasOne<AiPublicWidget>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.WidgetId })
            .HasPrincipalKey(e => new { e.TenantId, e.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
