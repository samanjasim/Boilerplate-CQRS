using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Identity.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(s => s.RefreshToken)
            .HasColumnName("refresh_token")
            .IsRequired();

        builder.Property(s => s.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(50);

        builder.Property(s => s.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(s => s.DeviceInfo)
            .HasColumnName("device_info")
            .HasMaxLength(200);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.LastActiveAt)
            .HasColumnName("last_active_at")
            .IsRequired();

        builder.Property(s => s.IsRevoked)
            .HasColumnName("is_revoked")
            .HasDefaultValue(false);

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.RefreshToken);
    }
}
