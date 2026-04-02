using System.Security.Cryptography;
using Starter.Domain.Common;

namespace Starter.Domain.Webhooks.Entities;

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

    public static WebhookEndpoint Create(
        string url, string? description, string events, Guid tenantId)
    {
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Convert.ToHexStringLower(secretBytes);

        return new WebhookEndpoint(
            Guid.NewGuid(),
            url.Trim(),
            description?.Trim(),
            secret,
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

    public string RegenerateSecret()
    {
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var newSecret = Convert.ToHexStringLower(secretBytes);
        Secret = newSecret;
        ModifiedAt = DateTime.UtcNow;
        return newSecret;
    }
}
