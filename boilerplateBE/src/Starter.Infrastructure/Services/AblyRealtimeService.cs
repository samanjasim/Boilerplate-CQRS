using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Settings;

namespace Starter.Infrastructure.Services;

public sealed class AblyRealtimeService : IRealtimeService
{
    private readonly HttpClient _httpClient;
    private readonly AblySettings _settings;
    private readonly ILogger<AblyRealtimeService> _logger;

    public AblyRealtimeService(
        HttpClient httpClient,
        IOptions<AblySettings> settings,
        ILogger<AblyRealtimeService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        var keyBytes = Encoding.ASCII.GetBytes(_settings.ApiKey);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(keyBytes));
    }

    public async Task PublishToUserAsync(Guid userId, string eventName, object data, CancellationToken ct = default)
    {
        try
        {
            var channel = $"user-{userId}";
            var url = $"https://rest.ably.io/channels/{channel}/messages";

            var payload = new
            {
                name = eventName,
                data = JsonSerializer.Serialize(data)
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Ably publish failed for channel {Channel}: {StatusCode} - {Body}",
                    channel, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish realtime notification to user {UserId}", userId);
        }
    }

    public async Task PublishToChannelAsync(string channel, string eventName, object data, CancellationToken ct = default)
    {
        try
        {
            var url = $"https://rest.ably.io/channels/{Uri.EscapeDataString(channel)}/messages";

            var payload = new
            {
                name = eventName,
                data = JsonSerializer.Serialize(data)
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Ably publish failed for channel {Channel}: {StatusCode} - {Body}",
                    channel, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish realtime event to channel {Channel}", channel);
        }
    }
}
