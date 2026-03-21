namespace Starter.Domain.Identity.Entities;

public sealed class LoginHistory
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string Email { get; private set; } = null!;
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? DeviceInfo { get; private set; }
    public bool Success { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private LoginHistory() { }

    public static LoginHistory Create(
        string email,
        Guid? userId,
        bool success,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        string? deviceInfo)
    {
        return new LoginHistory
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserId = userId,
            Success = success,
            FailureReason = failureReason,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceInfo = deviceInfo,
            CreatedAt = DateTime.UtcNow
        };
    }
}
