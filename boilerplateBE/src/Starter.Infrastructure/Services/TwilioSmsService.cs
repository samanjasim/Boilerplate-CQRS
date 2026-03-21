using System.Net.Http.Headers;
using System.Text;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Starter.Infrastructure.Services;

public sealed class TwilioSmsService : ISmsService
{
    private readonly TwilioSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TwilioSmsService> _logger;

    public TwilioSmsService(
        IOptions<TwilioSettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<TwilioSmsService> logger)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();

            var url = $"https://api.twilio.com/2010-04-01/Accounts/{_settings.AccountSid}/Messages.json";

            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{_settings.AccountSid}:{_settings.AuthToken}"));

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = phoneNumber,
                ["From"] = _settings.FromNumber,
                ["Body"] = message
            });

            var response = await client.PostAsync(url, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SMS sent successfully to {PhoneNumber}", phoneNumber);
                return true;
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Failed to send SMS to {PhoneNumber}. Status: {StatusCode}, Response: {Response}",
                    phoneNumber, response.StatusCode, responseBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", phoneNumber);
            return false;
        }
    }
}
