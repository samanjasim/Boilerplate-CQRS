using Starter.Domain.Tenants.Entities;
using Starter.Domain.Tenants.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Starter.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100);

        builder.HasIndex(t => t.Slug)
            .IsUnique();

        // Smart enum conversion for TenantStatus
        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasConversion(
                status => status.Name,
                name => TenantStatus.FromName(name)!)
            .IsRequired();

        builder.Property(t => t.ConnectionString)
            .HasColumnName("connection_string")
            .HasMaxLength(500);

        // Branding
        builder.Property(t => t.LogoFileId)
            .HasColumnName("logo_file_id");

        builder.Property(t => t.FaviconFileId)
            .HasColumnName("favicon_file_id");

        builder.Property(t => t.PrimaryColor)
            .HasColumnName("primary_color")
            .HasMaxLength(20);

        builder.Property(t => t.SecondaryColor)
            .HasColumnName("secondary_color")
            .HasMaxLength(20);

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        // Business Info
        builder.Property(t => t.Address)
            .HasColumnName("address")
            .HasMaxLength(500);

        builder.Property(t => t.Phone)
            .HasColumnName("phone")
            .HasMaxLength(50);

        builder.Property(t => t.Website)
            .HasColumnName("website")
            .HasMaxLength(200);

        builder.Property(t => t.TaxId)
            .HasColumnName("tax_id")
            .HasMaxLength(100);

        // Custom Text
        builder.Property(t => t.LoginPageTitle)
            .HasColumnName("login_page_title")
            .HasMaxLength(2000);

        builder.Property(t => t.LoginPageSubtitle)
            .HasColumnName("login_page_subtitle")
            .HasMaxLength(2000);

        builder.Property(t => t.EmailFooterText)
            .HasColumnName("email_footer_text")
            .HasMaxLength(2000);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(t => t.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(t => t.ModifiedBy)
            .HasColumnName("modified_by");

        // Registration
        builder.Property(t => t.DefaultRegistrationRoleId)
            .HasColumnName("default_registration_role_id");

        // Onboarding
        builder.Property(t => t.OnboardedAt)
            .HasColumnName("onboarded_at");
    }
}
