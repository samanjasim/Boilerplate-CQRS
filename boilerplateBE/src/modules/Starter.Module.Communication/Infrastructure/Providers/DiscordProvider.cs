using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Infrastructure.Providers;

internal sealed class DiscordProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<DiscordProvider> logger) : IIntegrationProvider
{
    public IntegrationType Type => IntegrationType.Discord;

    public async Task<ProviderDeliveryResult> SendAsync(IntegrationDeliveryRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var webhookUrl = request.ProviderCredentials.GetValueOrDefault("WebhookUrl", "");

            if (string.IsNullOrWhiteSpace(webhookUrl))
                return new ProviderDeliveryResult(false, null, "WebhookUrl credential is required.", 0);

            using var client = httpClientFactory.CreateClient();

            var payload = new { content = request.Message };
            var response = await client.PostAsJsonAsync(webhookUrl, payload, ct);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Discord message sent successfully in {Duration}ms", sw.ElapsedMilliseconds);
                return new ProviderDeliveryResult(true, null, null, (int)sw.ElapsedMilliseconds);
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Discord delivery failed: {StatusCode} - {Body}", response.StatusCode, errorBody);
            return new ProviderDeliveryResult(false, null, $"Discord returned {response.StatusCode}", (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "Discord delivery failed");
            return new ProviderDeliveryResult(false, null, ex.Message, (int)sw.ElapsedMilliseconds);
        }
    }
}
