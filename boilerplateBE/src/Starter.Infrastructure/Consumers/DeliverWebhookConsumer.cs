using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Messages;
using Starter.Domain.Webhooks.Entities;
using Starter.Infrastructure.Persistence;

namespace Starter.Infrastructure.Consumers;

public sealed class DeliverWebhookConsumer(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<DeliverWebhookConsumer> logger) : IConsumer<DeliverWebhookMessage>
{
    public async Task Consume(ConsumeContext<DeliverWebhookMessage> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Load all active endpoints for this tenant
        var allEndpoints = await dbContext.WebhookEndpoints
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == message.TenantId && e.IsActive)
            .ToListAsync(ct);

        // Filter in-memory: endpoints whose Events JSON array contains this event type
        var matchingEndpoints = allEndpoints
            .Where(e => EndpointSubscribesTo(e, message.EventType))
            .ToList();

        if (matchingEndpoints.Count == 0)
            return;

        var deliveries = new List<WebhookDelivery>();

        foreach (var endpoint in matchingEndpoints)
        {
            try
            {
                var delivery = await DeliverToEndpointAsync(
                    endpoint, message.EventType, message.Payload, ct);
                deliveries.Add(delivery);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Unexpected error delivering webhook to endpoint {EndpointId} for event {EventType}",
                    endpoint.Id, message.EventType);
            }
        }

        if (deliveries.Count > 0)
        {
            dbContext.WebhookDeliveries.AddRange(deliveries);
            await dbContext.SaveChangesAsync(ct);
        }
    }

    private async Task<WebhookDelivery> DeliverToEndpointAsync(
        WebhookEndpoint endpoint,
        string eventType,
        string payload,
        CancellationToken ct)
    {
        var delivery = WebhookDelivery.Create(endpoint.Id, eventType, payload, endpoint.TenantId);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(endpoint.Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var signature = $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            request.Headers.Add("X-Webhook-Signature-256", signature);
            request.Headers.Add("X-Webhook-Event", eventType);

            var response = await client.SendAsync(request, ct);
            stopwatch.Stop();

            var duration = (int)stopwatch.ElapsedMilliseconds;
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (responseBody.Length > 4096)
                responseBody = responseBody[..4096];

            if (response.IsSuccessStatusCode)
            {
                delivery.RecordSuccess((int)response.StatusCode, responseBody, duration);

                logger.LogInformation(
                    "Webhook delivered to {Url} for event {EventType} — status {StatusCode} in {Duration}ms",
                    endpoint.Url, eventType, (int)response.StatusCode, duration);
            }
            else
            {
                var errorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                delivery.RecordFailure((int)response.StatusCode, responseBody, errorMessage, duration);

                logger.LogWarning(
                    "Webhook delivery to {Url} failed for event {EventType} — status {StatusCode}",
                    endpoint.Url, eventType, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var duration = (int)stopwatch.ElapsedMilliseconds;
            var errorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;

            delivery.RecordFailure(null, null, errorMessage, duration);

            logger.LogError(ex,
                "Webhook delivery to {Url} threw exception for event {EventType}",
                endpoint.Url, eventType);
        }

        return delivery;
    }

    private static bool EndpointSubscribesTo(WebhookEndpoint endpoint, string eventType)
    {
        if (string.IsNullOrWhiteSpace(endpoint.Events))
            return false;

        try
        {
            var events = JsonSerializer.Deserialize<string[]>(endpoint.Events);
            return events?.Contains(eventType, StringComparer.OrdinalIgnoreCase) == true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
