using Starter.Domain.Common;
using Starter.Domain.Webhooks.Enums;

namespace Starter.Domain.Webhooks.Entities;

public sealed class WebhookDelivery : BaseEntity
{
    public Guid WebhookEndpointId { get; private set; }
    public string EventType { get; private set; } = default!;
    public string RequestPayload { get; private set; } = default!;
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public WebhookDeliveryStatus Status { get; private set; }
    public int? Duration { get; private set; }
    public int AttemptCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid TenantId { get; private set; }

    public WebhookEndpoint Endpoint { get; private set; } = default!;

    private WebhookDelivery() { }

    private WebhookDelivery(
        Guid id, Guid webhookEndpointId, string eventType,
        string requestPayload, Guid tenantId) : base(id)
    {
        WebhookEndpointId = webhookEndpointId;
        EventType = eventType;
        RequestPayload = requestPayload;
        Status = WebhookDeliveryStatus.Pending;
        AttemptCount = 1;
        TenantId = tenantId;
    }

    public static WebhookDelivery Create(
        Guid webhookEndpointId, string eventType, string requestPayload, Guid tenantId)
    {
        return new WebhookDelivery(Guid.NewGuid(), webhookEndpointId, eventType, requestPayload, tenantId);
    }

    public void RecordSuccess(int statusCode, string? responseBody, int duration)
    {
        ResponseStatusCode = statusCode;
        ResponseBody = responseBody;
        Duration = duration;
        Status = WebhookDeliveryStatus.Success;
        ModifiedAt = DateTime.UtcNow;
    }

    public void RecordFailure(int? statusCode, string? responseBody, string errorMessage, int duration)
    {
        ResponseStatusCode = statusCode;
        ResponseBody = responseBody;
        ErrorMessage = errorMessage;
        Duration = duration;
        Status = WebhookDeliveryStatus.Failed;
        ModifiedAt = DateTime.UtcNow;
    }
}
