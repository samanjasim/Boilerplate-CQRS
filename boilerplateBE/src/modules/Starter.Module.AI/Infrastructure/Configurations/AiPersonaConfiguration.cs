using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiPersonaConfiguration : IEntityTypeConfiguration<AiPersona>
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    public void Configure(EntityTypeBuilder<AiPersona> builder)
    {
        builder.ToTable("ai_personas");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.Slug).HasColumnName("slug").HasMaxLength(64).IsRequired();
        builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(120).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(e => e.AudienceType)
            .HasColumnName("audience_type").HasConversion<int>().IsRequired();
        builder.Property(e => e.SafetyPreset)
            .HasColumnName("safety_preset").HasConversion<int>().IsRequired();
        builder.Property(e => e.IsSystemReserved).HasColumnName("is_system_reserved").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.ModifiedBy).HasColumnName("modified_by");

        var stringListConverter = new ValueConverter<IReadOnlyList<string>, string>(
            v => JsonSerializer.Serialize(v, JsonOpts),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonOpts) ?? new List<string>());
        var stringListComparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v.ToList());

        builder.Property(e => e.PermittedAgentSlugs)
            .HasColumnName("permitted_agent_slugs")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter, stringListComparer)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Slug })
            .IsUnique()
            .HasDatabaseName("ix_ai_personas_tenant_slug");
        builder.HasIndex(e => new { e.TenantId, e.AudienceType })
            .HasDatabaseName("ix_ai_personas_tenant_audience");
        builder.HasIndex(e => new { e.TenantId, e.IsActive })
            .HasDatabaseName("ix_ai_personas_tenant_active");
    }
}
