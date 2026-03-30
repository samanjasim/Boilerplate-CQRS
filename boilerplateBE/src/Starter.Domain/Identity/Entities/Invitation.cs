using Starter.Domain.Common;

namespace Starter.Domain.Identity.Entities;

public sealed class Invitation : BaseAuditableEntity
{
    public const int TokenLength = 64;

    public string Email { get; private set; } = null!;
    public string Token { get; private set; } = null!;
    public Guid RoleId { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid InvitedBy { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsAccepted { get; private set; }
    public DateTime? AcceptedAt { get; private set; }

    private Invitation() { }

    public static Invitation Create(string email, Guid roleId, Guid? tenantId, Guid invitedBy, int expirationDays = 7)
    {
        return new Invitation(Guid.NewGuid())
        {
            Email = email.ToLowerInvariant().Trim(),
            Token = GenerateToken(),
            RoleId = roleId,
            TenantId = tenantId,
            InvitedBy = invitedBy,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            IsAccepted = false
        };
    }

    private Invitation(Guid id) : base(id) { }

    public void Accept()
    {
        IsAccepted = true;
        AcceptedAt = DateTime.UtcNow;
    }

    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;

    public bool IsValid() => !IsAccepted && !IsExpired();

    private static string GenerateToken()
    {
        var bytes = new byte[TokenLength / 2];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
