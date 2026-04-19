using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        builder.ToTable("ai_messages");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(e => e.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Content)
            .HasColumnName("content");

        builder.Property(e => e.ToolCalls)
            .HasColumnName("tool_calls")
            .HasColumnType("jsonb");

        builder.Property(e => e.ToolCallId)
            .HasColumnName("tool_call_id")
            .HasMaxLength(200);

        builder.Property(e => e.InputTokens)
            .HasColumnName("input_tokens")
            .IsRequired();

        builder.Property(e => e.OutputTokens)
            .HasColumnName("output_tokens")
            .IsRequired();

        builder.Property(e => e.Order)
            .HasColumnName("order")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        var citationsConverter = new ValueConverter<IReadOnlyList<AiMessageCitation>, string>(
            v => JsonSerializer.Serialize(v, JsonOpts),
            v => JsonSerializer.Deserialize<List<AiMessageCitation>>(v, JsonOpts) ?? new List<AiMessageCitation>());
        var citationsComparer = new ValueComparer<IReadOnlyList<AiMessageCitation>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, c) => HashCode.Combine(hash, c.GetHashCode())),
            v => v.ToList());

        builder.Property(e => e.Citations)
            .HasColumnName("citations")
            .HasColumnType("jsonb")
            .HasConversion(citationsConverter, citationsComparer)
            .IsRequired();

        builder.HasIndex(e => new { e.ConversationId, e.Order });
    }
}
