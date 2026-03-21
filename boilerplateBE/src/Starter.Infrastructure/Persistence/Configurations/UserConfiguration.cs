using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Enums;
using Starter.Domain.Identity.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(u => u.Username)
            .HasColumnName("username")
            .HasMaxLength(User.MaxUsernameLength)
            .IsRequired();

        builder.HasIndex(u => u.Username)
            .IsUnique();

        // Owned value object: Email
        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(Starter.Domain.Identity.ValueObjects.Email.MaxLength)
                .IsRequired();

            email.HasIndex(e => e.Value)
                .IsUnique();
        });

        // Owned value object: FullName
        builder.OwnsOne(u => u.FullName, fullName =>
        {
            fullName.Property(f => f.FirstName)
                .HasColumnName("first_name")
                .HasMaxLength(FullName.MaxFirstNameLength)
                .IsRequired();

            fullName.Property(f => f.LastName)
                .HasColumnName("last_name")
                .HasMaxLength(FullName.MaxLastNameLength)
                .IsRequired();
        });

        // Owned value object: PhoneNumber (optional)
        builder.OwnsOne(u => u.PhoneNumber, phone =>
        {
            phone.Property(p => p.Value)
                .HasColumnName("phone_number")
                .HasMaxLength(PhoneNumber.MaxLength);
        });

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        // Smart enum conversion for UserStatus
        builder.Property(u => u.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasConversion(
                status => status.Name,
                name => UserStatus.FromName(name)!)
            .IsRequired();

        builder.Property(u => u.EmailConfirmed)
            .HasColumnName("email_confirmed")
            .HasDefaultValue(false);

        builder.Property(u => u.PhoneConfirmed)
            .HasColumnName("phone_confirmed")
            .HasDefaultValue(false);

        builder.Property(u => u.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(u => u.FailedLoginAttempts)
            .HasColumnName("failed_login_attempts")
            .HasDefaultValue(0);

        builder.Property(u => u.LockoutEndAt)
            .HasColumnName("lockout_end_at");

        builder.Property(u => u.RefreshToken)
            .HasColumnName("refresh_token");

        builder.Property(u => u.RefreshTokenExpiresAt)
            .HasColumnName("refresh_token_expires_at");

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(u => u.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(u => u.ModifiedBy)
            .HasColumnName("modified_by");

        builder.Property(u => u.TenantId)
            .HasColumnName("tenant_id");
        builder.HasIndex(u => u.TenantId);

        builder.Navigation(u => u.UserRoles)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
