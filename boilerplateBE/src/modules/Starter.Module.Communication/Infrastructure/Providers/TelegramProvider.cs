using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Infrastructure.Providers;

internal sealed class TelegramProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<TelegramProvider> logger) : IIntegrationProvider
{
    public IntegrationType Type => IntegrationType.Telegram;

    public async Task<ProviderDeliveryResult> SendAsync(IntegrationDeliveryRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var botToken = request.ProviderCredentials.GetValueOrDefault("BotToken", "");
            var chatId = request.TargetChannelId != "test"
                ? request.TargetChannelId
                : request.ProviderCredentials.GetValueOrDefault("ChatId", "");

            if (string.IsNullOrWhiteSpace(botToken))
                return new ProviderDeliveryResult(false, null, "BotToken credential is required.", 0);

            if (string.IsNullOrWhiteSpace(chatId))
                return new ProviderDeliveryResult(false, null, "ChatId is required.", 0);

            using var client = httpClientFactory.CreateClient();
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

            var payload = new
            {
                chat_id = chatId,
                text = request.Message,
                parse_mode = "HTML"
            };

            var response = await client.PostAsJsonAsync(url, payload, ct);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Telegram message sent to {ChatId} in {Duration}ms", chatId, sw.ElapsedMilliseconds);
                return new ProviderDeliveryResult(true, null, null, (int)sw.ElapsedMilliseconds);
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Telegram delivery failed: {StatusCode} - {Body}", response.StatusCode, errorBody);
            return new ProviderDeliveryResult(false, null, $"Telegram returned {response.StatusCode}", (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "Telegram delivery failed");
            return new ProviderDeliveryResult(false, null, ex.Message, (int)sw.ElapsedMilliseconds);
        }
    }
}
