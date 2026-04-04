using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.ImportExport.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("import_jobs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.FileId)
            .HasColumnName("file_id")
            .IsRequired();

        builder.Property(e => e.ConflictMode)
            .HasColumnName("conflict_mode")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(e => e.TotalRows)
            .HasColumnName("total_rows")
            .IsRequired();

        builder.Property(e => e.ProcessedRows)
            .HasColumnName("processed_rows")
            .IsRequired();

        builder.Property(e => e.CreatedCount)
            .HasColumnName("created_count")
            .IsRequired();

        builder.Property(e => e.UpdatedCount)
            .HasColumnName("updated_count")
            .IsRequired();

        builder.Property(e => e.SkippedCount)
            .HasColumnName("skipped_count")
            .IsRequired();

        builder.Property(e => e.FailedCount)
            .HasColumnName("failed_count")
            .IsRequired();

        builder.Property(e => e.ResultsFileId)
            .HasColumnName("results_file_id");

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.Property(e => e.RequestedBy)
            .HasColumnName("requested_by")
            .IsRequired();

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at");

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(e => e.ModifiedBy)
            .HasColumnName("modified_by");

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.Status });
    }
}
