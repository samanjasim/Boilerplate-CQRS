using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.CommentsActivity.Domain.Entities;

namespace Starter.Module.CommentsActivity.Infrastructure.Configurations;

internal sealed class EntityWatcherConfiguration : IEntityTypeConfiguration<EntityWatcher>
{
    public void Configure(EntityTypeBuilder<EntityWatcher> builder)
    {
        builder.ToTable("entity_watchers");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(w => w.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(w => w.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(w => w.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(w => w.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(w => w.Reason)
            .HasColumnName("reason")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(w => w.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(w => w.ModifiedAt)
            .HasColumnName("modified_at");

        builder.HasIndex(w => new { w.EntityType, w.EntityId, w.UserId })
            .IsUnique();

        builder.HasIndex(w => w.UserId);
        builder.HasIndex(w => w.TenantId);
    }
}
