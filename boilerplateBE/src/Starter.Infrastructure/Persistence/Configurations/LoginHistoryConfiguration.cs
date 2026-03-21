using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Identity.Entities;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> builder)
    {
        builder.ToTable("login_history");

        builder.HasKey(lh => lh.Id);

        builder.Property(lh => lh.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(lh => lh.UserId)
            .HasColumnName("user_id");

        builder.Property(lh => lh.Email)
            .HasColumnName("email")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(lh => lh.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(50);

        builder.Property(lh => lh.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(lh => lh.DeviceInfo)
            .HasColumnName("device_info")
            .HasMaxLength(200);

        builder.Property(lh => lh.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(lh => lh.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(100);

        builder.Property(lh => lh.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(lh => lh.UserId);
        builder.HasIndex(lh => lh.Email);
        builder.HasIndex(lh => lh.CreatedAt);
    }
}
