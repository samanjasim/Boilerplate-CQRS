using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Infrastructure.Configurations;

internal sealed class MessageTemplateOverrideConfiguration : IEntityTypeConfiguration<MessageTemplateOverride>
{
    public void Configure(EntityTypeBuilder<MessageTemplateOverride> builder)
    {
        builder.ToTable("communication_message_template_overrides");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.MessageTemplateId).HasColumnName("message_template_id").IsRequired();
        builder.Property(e => e.SubjectTemplate).HasColumnName("subject_template").HasMaxLength(500);
        builder.Property(e => e.BodyTemplate).HasColumnName("body_template").HasColumnType("text").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ModifiedAt).HasColumnName("modified_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.ModifiedBy).HasColumnName("modified_by");
        builder.HasIndex(e => new { e.TenantId, e.MessageTemplateId }).IsUnique();
        builder.HasIndex(e => e.TenantId);
        builder.HasOne<MessageTemplate>()
            .WithMany()
            .HasForeignKey(e => e.MessageTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
