using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.CommentsActivity.Domain.Entities;

namespace Starter.Module.CommentsActivity.Infrastructure.Configurations;

internal sealed class ActivityEntryConfiguration : IEntityTypeConfiguration<ActivityEntry>
{
    public void Configure(EntityTypeBuilder<ActivityEntry> builder)
    {
        builder.ToTable("activity_entries");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(a => a.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(a => a.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(a => a.Action)
            .HasColumnName("action")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.ActorId)
            .HasColumnName("actor_id");

        builder.Property(a => a.MetadataJson)
            .HasColumnName("metadata_json")
            .HasMaxLength(4000);

        builder.Property(a => a.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.ModifiedAt)
            .HasColumnName("modified_at");

        builder.HasIndex(a => new { a.EntityType, a.EntityId, a.CreatedAt })
            .IsDescending(false, false, true);

        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => a.ActorId);
    }
}
