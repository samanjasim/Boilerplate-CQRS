using Starter.Domain.Common;

namespace Starter.Domain.Identity.Entities;

public sealed class Session
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string RefreshToken { get; private set; } = null!;
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? DeviceInfo { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; private set; } = DateTime.UtcNow;
    public bool IsRevoked { get; private set; }

    private Session() { }

    public static Session Create(
        Guid userId,
        string refreshToken,
        string? ipAddress,
        string? userAgent)
    {
        return new Session
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RefreshToken = refreshToken,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceInfo = DeviceInfoParser.Parse(userAgent),
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow,
            IsRevoked = false
        };
    }

    public void UpdateRefreshToken(string newRefreshToken)
    {
        RefreshToken = newRefreshToken;
        LastActiveAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        IsRevoked = true;
    }

}
