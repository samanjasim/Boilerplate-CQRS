using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiAssistantConfiguration : IEntityTypeConfiguration<AiAssistant>
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    public void Configure(EntityTypeBuilder<AiAssistant> builder)
    {
        builder.ToTable("ai_assistants");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description");

        builder.Property(e => e.SystemPrompt)
            .HasColumnName("system_prompt")
            .IsRequired();

        builder.Property(e => e.Provider)
            .HasColumnName("provider")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.Model)
            .HasColumnName("model")
            .HasMaxLength(100);

        builder.Property(e => e.Temperature)
            .HasColumnName("temperature")
            .IsRequired();

        builder.Property(e => e.MaxTokens)
            .HasColumnName("max_tokens")
            .IsRequired();

        var stringListConverter = new ValueConverter<IReadOnlyList<string>, string>(
            v => JsonSerializer.Serialize(v, JsonOpts),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonOpts) ?? new List<string>());
        var stringListComparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v.ToList());

        builder.Property(e => e.EnabledToolNames)
            .HasColumnName("enabled_tool_names")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter, stringListComparer)
            .IsRequired();

        var guidListConverter = new ValueConverter<IReadOnlyList<Guid>, string>(
            v => JsonSerializer.Serialize(v, JsonOpts),
            v => JsonSerializer.Deserialize<List<Guid>>(v, JsonOpts) ?? new List<Guid>());
        var guidListComparer = new ValueComparer<IReadOnlyList<Guid>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, g) => HashCode.Combine(hash, g.GetHashCode())),
            v => v.ToList());

        builder.Property(e => e.KnowledgeBaseDocIds)
            .HasColumnName("knowledge_base_doc_ids")
            .HasColumnType("jsonb")
            .HasConversion(guidListConverter, guidListComparer)
            .IsRequired();

        builder.Property(e => e.Slug)
            .HasColumnName("slug")
            .HasMaxLength(64)
            .IsRequired()
            .HasDefaultValue("");

        builder.Property(e => e.PersonaTargetSlugs)
            .HasColumnName("persona_target_slugs")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter, stringListComparer)
            .IsRequired();

        builder.Property(e => e.ExecutionMode)
            .HasColumnName("execution_mode")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.RagScope)
            .HasColumnName("rag_scope")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.MaxAgentSteps)
            .HasColumnName("max_agent_steps")
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(e => e.Visibility)
            .HasColumnName("visibility")
            .HasConversion<int>()
            .HasDefaultValue(ResourceVisibility.Private)
            .IsRequired();

        builder.Property(e => e.AccessMode)
            .HasColumnName("access_mode")
            .HasConversion<int>()
            .HasDefaultValue(AssistantAccessMode.CallerPrincipal)
            .IsRequired();

        builder.Property(e => e.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(e => e.TemplateSourceSlug)
            .HasColumnName("template_source_slug")
            .HasMaxLength(128);

        builder.Property(e => e.TemplateSourceVersion)
            .HasColumnName("template_source_version")
            .HasMaxLength(32);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(e => e.ModifiedBy)
            .HasColumnName("modified_by");

        builder.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique();

        builder.HasIndex(e => new { e.TenantId, e.CreatedByUserId });
        builder.HasIndex(e => new { e.TenantId, e.Visibility });

        builder.HasIndex(e => new { e.TenantId, e.Slug })
            .IsUnique()
            .HasFilter("slug <> ''")
            .HasDatabaseName("ux_ai_assistants_tenant_slug");
    }
}
