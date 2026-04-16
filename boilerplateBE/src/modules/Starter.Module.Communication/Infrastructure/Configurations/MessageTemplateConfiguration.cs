using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Infrastructure.Configurations;

internal sealed class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        builder.ToTable("communication_message_templates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(e => e.ModuleSource).HasColumnName("module_source").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Category).HasColumnName("category").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(e => e.SubjectTemplate).HasColumnName("subject_template").HasMaxLength(500);
        builder.Property(e => e.BodyTemplate).HasColumnName("body_template").HasColumnType("text").IsRequired();
        builder.Property(e => e.DefaultChannel).HasColumnName("default_channel").IsRequired();
        builder.Property(e => e.AvailableChannelsJson).HasColumnName("available_channels_json").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.VariableSchemaJson).HasColumnName("variable_schema_json").HasColumnType("jsonb");
        builder.Property(e => e.SampleVariablesJson).HasColumnName("sample_variables_json").HasColumnType("jsonb");
        builder.Property(e => e.IsSystem).HasColumnName("is_system").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => e.Category);
    }
}
