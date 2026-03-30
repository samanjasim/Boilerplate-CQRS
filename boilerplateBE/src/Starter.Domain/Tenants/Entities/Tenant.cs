using Starter.Domain.Common;
using Starter.Domain.Tenants.Enums;
using Starter.Domain.Tenants.Events;

namespace Starter.Domain.Tenants.Entities;

public sealed class Tenant : AggregateRoot
{
    public string Name { get; private set; } = null!;
    public string? Slug { get; private set; }
    public TenantStatus Status { get; private set; } = null!;
    public string? ConnectionString { get; private set; }

    // Branding
    public Guid? LogoFileId { get; private set; }
    public Guid? FaviconFileId { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public string? Description { get; private set; }

    // Registration
    public Guid? DefaultRegistrationRoleId { get; private set; }

    // Business Info
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Website { get; private set; }
    public string? TaxId { get; private set; }

    // Custom Text (JSON: {"en": "...", "ar": "...", "ku": "..."})
    public string? LoginPageTitle { get; private set; }
    public string? LoginPageSubtitle { get; private set; }
    public string? EmailFooterText { get; private set; }

    private Tenant() { }

    private Tenant(
        Guid id,
        string name,
        string? slug,
        string? connectionString) : base(id)
    {
        Name = name;
        Slug = slug;
        Status = TenantStatus.Active;
        ConnectionString = connectionString;
    }

    public static Tenant Create(
        string name,
        string? slug = null,
        string? connectionString = null)
    {
        var tenant = new Tenant(
            Guid.NewGuid(),
            name,
            slug,
            connectionString);

        tenant.RaiseDomainEvent(new TenantCreatedEvent(tenant.Id));

        return tenant;
    }

    public void Update(string name, string? slug)
    {
        Name = name;
        Slug = slug;
    }

    public void UpdateBranding(Guid? logoFileId, Guid? faviconFileId, string? primaryColor, string? secondaryColor, string? description)
    {
        LogoFileId = logoFileId;
        FaviconFileId = faviconFileId;
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
        Description = description;
    }

    public void UpdateBusinessInfo(string? address, string? phone, string? website, string? taxId)
    {
        Address = address;
        Phone = phone;
        Website = website;
        TaxId = taxId;
    }

    public void UpdateCustomText(string? loginPageTitle, string? loginPageSubtitle, string? emailFooterText)
    {
        LoginPageTitle = loginPageTitle;
        LoginPageSubtitle = loginPageSubtitle;
        EmailFooterText = emailFooterText;
    }

    public void SetDefaultRegistrationRole(Guid? roleId)
    {
        DefaultRegistrationRoleId = roleId;
    }

    public void Activate()
    {
        Status = TenantStatus.Active;
    }

    public void Deactivate()
    {
        Status = TenantStatus.Inactive;
    }

    public void Suspend()
    {
        Status = TenantStatus.Suspended;
    }
}
