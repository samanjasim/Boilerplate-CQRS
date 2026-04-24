using System.Security.Cryptography;
using Starter.Domain.Common;

namespace Starter.Module.Webhooks.Domain.Entities;

public sealed class WebhookEndpoint : AggregateRoot
{
    public string Url { get; private set; } = default!;
    public string? Description { get; private set; }
    public string Secret { get; private set; } = default!;
    public string Events { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public Guid TenantId { get; private set; }

    private readonly List<WebhookDelivery> _deliveries = [];
    public IReadOnlyCollection<WebhookDelivery> Deliveries => _deliveries.AsReadOnly();

    private WebhookEndpoint() { }

    private WebhookEndpoint(
        Guid id, string url, string? description, string secret,
        string events, bool isActive, Guid tenantId) : base(id)
    {
        Url = url;
        Description = description;
        Secret = secret;
        Events = events;
        IsActive = isActive;
        TenantId = tenantId;
    }

    /// <summary>Generates a fresh cryptographic secret. Callers are responsible for
    /// protecting (encrypting) the value before storing it via <see cref="Create"/>.</summary>
    public static string GenerateSecret()
    {
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexStringLower(secretBytes);
    }

    public static WebhookEndpoint Create(
        string url, string? description, string storedSecret, string events, Guid tenantId)
    {
        return new WebhookEndpoint(
            Guid.NewGuid(),
            url.Trim(),
            description?.Trim(),
            storedSecret,
            events,
            true,
            tenantId);
    }

    public void Update(string url, string? description, string events, bool isActive)
    {
        Url = url.Trim();
        Description = description?.Trim();
        Events = events;
        IsActive = isActive;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        ModifiedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>Replaces the stored secret. The caller supplies the already-protected
    /// value; this keeps the domain entity free of any cryptography abstractions.</summary>
    public void ReplaceSecret(string storedSecret)
    {
        Secret = storedSecret;
        ModifiedAt = DateTime.UtcNow;
    }
}
