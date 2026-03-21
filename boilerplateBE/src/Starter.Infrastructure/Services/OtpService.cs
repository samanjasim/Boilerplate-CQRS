using System.Security.Cryptography;
using Starter.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Starter.Infrastructure.Services;

public sealed class OtpService : IOtpService
{
    private readonly ICacheService _cache;
    private readonly ILogger<OtpService> _logger;

    private static readonly TimeSpan OtpExpiration = TimeSpan.FromMinutes(10);
    private const int MaxAttempts = 3;

    public OtpService(ICacheService cache, ILogger<OtpService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(
        string purpose,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        var rateLimitKey = $"otp-rate:{purpose}:{identifier}";
        var otpKey = $"otp:{purpose}:{identifier}";

        var attempts = await _cache.GetAsync<int?>(rateLimitKey, cancellationToken) ?? 0;

        if (attempts >= MaxAttempts)
        {
            _logger.LogWarning(
                "OTP rate limit exceeded for {Purpose}:{Identifier}",
                purpose, identifier);

            throw new InvalidOperationException("Too many OTP requests. Please try again later.");
        }

        var code = GenerateCode();

        await _cache.SetAsync(otpKey, code, OtpExpiration, cancellationToken);
        await _cache.SetAsync(rateLimitKey, attempts + 1, OtpExpiration, cancellationToken);

        _logger.LogInformation(
            "OTP generated for {Purpose}:{Identifier}",
            purpose, identifier);

        return code;
    }

    public async Task<bool> ValidateAsync(
        string purpose,
        string identifier,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        var otpKey = $"otp:{purpose}:{identifier}";

        var storedCode = await _cache.GetAsync<string>(otpKey, cancellationToken);

        if (storedCode is null)
        {
            _logger.LogDebug("No OTP found for {Purpose}:{Identifier}", purpose, identifier);
            return false;
        }

        if (!string.Equals(storedCode, code, StringComparison.Ordinal))
        {
            _logger.LogDebug("OTP mismatch for {Purpose}:{Identifier}", purpose, identifier);
            return false;
        }

        await _cache.RemoveAsync(otpKey, cancellationToken);

        _logger.LogInformation(
            "OTP validated successfully for {Purpose}:{Identifier}",
            purpose, identifier);

        return true;
    }

    private static string GenerateCode()
    {
        return RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
    }
}
