using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiPublicWidgetConfiguration : IEntityTypeConfiguration<AiPublicWidget>
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    public void Configure(EntityTypeBuilder<AiPublicWidget> builder)
    {
        builder.ToTable("ai_public_widgets");
        builder.HasKey(e => e.Id);

        var stringListConverter = new ValueConverter<IReadOnlyList<string>, string>(
            v => JsonSerializer.Serialize(v, JsonOpts),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonOpts) ?? new List<string>());
        var stringListComparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v.ToList());

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(e => e.AllowedOrigins)
            .HasColumnName("allowed_origins")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter, stringListComparer)
            .IsRequired();
        builder.Property(e => e.DefaultAssistantId).HasColumnName("default_assistant_id");
        builder.Property(e => e.DefaultPersonaSlug).HasColumnName("default_persona_slug").HasMaxLength(128).IsRequired();
        builder.Property(e => e.MonthlyTokenCap).HasColumnName("monthly_token_cap");
        builder.Property(e => e.DailyTokenCap).HasColumnName("daily_token_cap");
        builder.Property(e => e.RequestsPerMinute).HasColumnName("requests_per_minute");
        builder.Property(e => e.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
        builder.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.ModifiedBy).HasColumnName("modified_by");

        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName("ix_ai_public_widgets_tenant_status");
    }
}
