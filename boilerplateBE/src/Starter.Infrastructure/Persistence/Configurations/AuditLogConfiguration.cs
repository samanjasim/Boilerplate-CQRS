using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Common;

namespace Starter.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.EntityType).IsRequired();
        builder.Property(a => a.EntityId).IsRequired();
        builder.Property(a => a.Action).IsRequired();
        builder.Property(a => a.Changes).HasColumnType("jsonb");
        builder.Property(a => a.PerformedByName).HasMaxLength(200);
        builder.Property(a => a.IpAddress).HasMaxLength(50);
        builder.Property(a => a.CorrelationId).HasMaxLength(100);

        builder.HasIndex(a => a.EntityType);
        builder.HasIndex(a => a.EntityId);
        builder.HasIndex(a => a.PerformedAt);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.PerformedBy);
    }
}
