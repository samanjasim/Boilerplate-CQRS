using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Workflow.Domain.Entities;

namespace Starter.Module.Workflow.Infrastructure.Persistence.Configurations;

public sealed class DelegationRuleConfiguration : IEntityTypeConfiguration<DelegationRule>
{
    public void Configure(EntityTypeBuilder<DelegationRule> builder)
    {
        builder.ToTable("workflow_delegation_rules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(r => r.FromUserId)
            .HasColumnName("from_user_id")
            .IsRequired();

        builder.Property(r => r.ToUserId)
            .HasColumnName("to_user_id")
            .IsRequired();

        builder.Property(r => r.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(r => r.EndDate)
            .HasColumnName("end_date")
            .IsRequired();

        builder.Property(r => r.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(r => r.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(r => r.ModifiedBy)
            .HasColumnName("modified_by");

        // Tenant query filter applied in WorkflowDbContext.OnModelCreating.
        // Unique index prevents overlapping delegation rules for the same delegator and date range.
        builder.HasIndex(r => new { r.FromUserId, r.StartDate, r.EndDate })
            .IsUnique();

        builder.HasIndex(r => new { r.TenantId, r.FromUserId, r.IsActive });
    }
}
