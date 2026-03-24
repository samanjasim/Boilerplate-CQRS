using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class ReportRequestConfiguration : IEntityTypeConfiguration<ReportRequest>
{
    public void Configure(EntityTypeBuilder<ReportRequest> builder)
    {
        builder.ToTable("report_requests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(r => r.RequestedBy)
            .HasColumnName("requested_by")
            .IsRequired();

        // Smart enum conversion for ReportType
        builder.Property(r => r.ReportType)
            .HasColumnName("report_type")
            .HasMaxLength(50)
            .HasConversion(
                rt => rt.Name,
                name => ReportType.FromName(name)!)
            .IsRequired();

        // Smart enum conversion for ReportFormat
        builder.Property(r => r.Format)
            .HasColumnName("format")
            .HasMaxLength(50)
            .HasConversion(
                f => f.Name,
                name => ReportFormat.FromName(name)!)
            .IsRequired();

        builder.Property(r => r.Filters)
            .HasColumnName("filters")
            .HasMaxLength(4000);

        // Smart enum conversion for ReportStatus
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasConversion(
                s => s.Name,
                name => ReportStatus.FromName(name)!)
            .IsRequired();

        builder.Property(r => r.FileId)
            .HasColumnName("file_id");

        builder.Property(r => r.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(500);

        builder.Property(r => r.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.Property(r => r.RequestedAt)
            .HasColumnName("requested_at")
            .IsRequired();

        builder.Property(r => r.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(r => r.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(r => r.FilterHash)
            .HasColumnName("filter_hash")
            .HasMaxLength(128);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(r => r.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(r => r.ModifiedBy)
            .HasColumnName("modified_by");

        builder.HasIndex(r => new { r.FilterHash, r.TenantId });
        builder.HasIndex(r => r.RequestedBy);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.CreatedAt);
        builder.HasIndex(r => new { r.TenantId, r.RequestedBy });
    }
}
