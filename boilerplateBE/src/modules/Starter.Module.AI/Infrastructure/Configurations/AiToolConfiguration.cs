using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Configurations;

internal sealed class AiToolConfiguration : IEntityTypeConfiguration<AiTool>
{
    public void Configure(EntityTypeBuilder<AiTool> builder)
    {
        builder.ToTable("ai_tools");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.ParameterSchema)
            .HasColumnName("parameter_schema")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.CommandType)
            .HasColumnName("command_type")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.RequiredPermission)
            .HasColumnName("required_permission")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Category)
            .HasColumnName("category")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired();

        builder.Property(e => e.IsReadOnly)
            .HasColumnName("is_read_only")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.HasIndex(e => e.Name)
            .IsUnique();
    }
}
