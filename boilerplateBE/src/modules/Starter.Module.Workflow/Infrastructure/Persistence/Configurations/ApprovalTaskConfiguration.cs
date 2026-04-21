using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Module.Workflow.Domain.Entities;

namespace Starter.Module.Workflow.Infrastructure.Persistence.Configurations;

public sealed class ApprovalTaskConfiguration : IEntityTypeConfiguration<ApprovalTask>
{
    public void Configure(EntityTypeBuilder<ApprovalTask> builder)
    {
        builder.ToTable("workflow_approval_tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(t => t.InstanceId)
            .HasColumnName("instance_id")
            .IsRequired();

        builder.Property(t => t.StepName)
            .HasColumnName("step_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.AssigneeUserId)
            .HasColumnName("assignee_user_id");

        builder.Property(t => t.AssigneeRole)
            .HasColumnName("assignee_role")
            .HasMaxLength(200);

        builder.Property(t => t.AssigneeStrategyJson)
            .HasColumnName("assignee_strategy_json")
            .HasColumnType("text");

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(t => t.Action)
            .HasColumnName("action")
            .HasMaxLength(200);

        builder.Property(t => t.Comment)
            .HasColumnName("comment")
            .HasMaxLength(4000);

        builder.Property(t => t.DueDate)
            .HasColumnName("due_date");

        builder.Property(t => t.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(t => t.CompletedByUserId)
            .HasColumnName("completed_by_user_id");

        builder.Property(t => t.GroupId)
            .HasColumnName("group_id");

        builder.Property(t => t.ReminderSentAt)
            .HasColumnName("reminder_sent_at");

        builder.Property(t => t.EscalatedAt)
            .HasColumnName("escalated_at");

        builder.Property(t => t.OriginalAssigneeUserId)
            .HasColumnName("original_assignee_user_id");

        builder.Property(t => t.DefinitionName)
            .HasColumnName("definition_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.DefinitionDisplayName)
            .HasColumnName("definition_display_name")
            .HasMaxLength(200);

        builder.Property(t => t.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(t => t.EntityDisplayName)
            .HasColumnName("entity_display_name")
            .HasMaxLength(200);

        builder.Property(t => t.FormFieldsJson)
            .HasColumnName("form_fields_json")
            .HasColumnType("text");

        builder.Property(t => t.AvailableActionsJson)
            .HasColumnName("available_actions_json")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(t => t.SlaReminderAfterHours)
            .HasColumnName("sla_reminder_after_hours");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(t => t.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(t => t.ModifiedBy)
            .HasColumnName("modified_by");

        builder.Property(t => t.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        // Most important: inbox query — find all pending tasks for a user
        builder.HasIndex(t => new { t.AssigneeUserId, t.Status });
        builder.HasIndex(t => new { t.InstanceId, t.Status });
        builder.HasIndex(t => new { t.TenantId, t.Status });
        builder.HasIndex(t => t.GroupId);
    }
}
