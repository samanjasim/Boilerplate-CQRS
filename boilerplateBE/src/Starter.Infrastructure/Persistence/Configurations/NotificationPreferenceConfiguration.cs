using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Common;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");

        builder.HasKey(np => np.Id);

        builder.Property(np => np.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(np => np.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(np => np.NotificationType)
            .HasColumnName("notification_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(np => np.EmailEnabled)
            .HasColumnName("email_enabled")
            .HasDefaultValue(true);

        builder.Property(np => np.InAppEnabled)
            .HasColumnName("in_app_enabled")
            .HasDefaultValue(true);

        builder.HasIndex(np => np.UserId);
        builder.HasIndex(np => new { np.UserId, np.NotificationType }).IsUnique();
    }
}
