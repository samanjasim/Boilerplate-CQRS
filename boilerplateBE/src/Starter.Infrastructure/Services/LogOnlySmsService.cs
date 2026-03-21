using Starter.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Starter.Infrastructure.Services;

public sealed class LogOnlySmsService : ISmsService
{
    private readonly ILogger<LogOnlySmsService> _logger;

    public LogOnlySmsService(ILogger<LogOnlySmsService> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SMS would be sent to {PhoneNumber}: {Message}",
            phoneNumber, message);

        return Task.FromResult(true);
    }
}
