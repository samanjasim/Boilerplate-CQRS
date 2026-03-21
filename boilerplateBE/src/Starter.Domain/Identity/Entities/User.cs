using Starter.Domain.Common;
using Starter.Domain.Identity.Enums;
using Starter.Domain.Identity.Events;
using Starter.Domain.Identity.ValueObjects;

namespace Starter.Domain.Identity.Entities;

public sealed class User : AggregateRoot
{
    public const int MaxUsernameLength = 100;
    public const int DefaultMaxFailedAttempts = 5;
    public const int DefaultLockoutMinutes = 15;

    public string Username { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public FullName FullName { get; private set; } = null!;
    public PhoneNumber? PhoneNumber { get; private set; }
    public string PasswordHash { get; private set; } = null!;
    public UserStatus Status { get; private set; } = null!;
    public bool EmailConfirmed { get; private set; }
    public bool PhoneConfirmed { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutEndAt { get; private set; }
    public Guid? TenantId { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiresAt { get; private set; }

    private readonly List<UserRole> _userRoles = [];
    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    private User() { }

    private User(
        Guid id,
        string username,
        Email email,
        FullName fullName,
        string passwordHash,
        Guid? tenantId = null) : base(id)
    {
        Username = username;
        Email = email;
        FullName = fullName;
        PasswordHash = passwordHash;
        TenantId = tenantId;
        Status = UserStatus.Pending;
        EmailConfirmed = false;
        PhoneConfirmed = false;
        FailedLoginAttempts = 0;
    }

    public static User Create(
        string username,
        Email email,
        FullName fullName,
        string passwordHash,
        Guid? tenantId = null)
    {
        var user = new User(
            Guid.NewGuid(),
            username,
            email,
            fullName,
            passwordHash,
            tenantId);

        user.RaiseDomainEvent(new UserCreatedEvent(user.Id, user.Email.Value, user.FullName.GetFullName()));

        return user;
    }

    public void UpdateProfile(FullName fullName, PhoneNumber? phoneNumber, Email? newEmail = null)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;

        if (newEmail is not null && newEmail != Email)
        {
            Email = newEmail;
            EmailConfirmed = false;
        }

        RaiseDomainEvent(new UserUpdatedEvent(Id));
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        RaiseDomainEvent(new PasswordChangedEvent(Id));
    }

    public void ConfirmEmail()
    {
        EmailConfirmed = true;
        if (Status == UserStatus.Pending)
            Status = UserStatus.Active;
    }

    public void ConfirmPhone()
    {
        PhoneConfirmed = true;
    }

    public void RecordSuccessfulLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockoutEndAt = null;
    }

    public void RecordFailedLogin(
        int maxFailedAttempts = DefaultMaxFailedAttempts,
        int lockoutMinutes = DefaultLockoutMinutes)
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts >= maxFailedAttempts)
        {
            LockoutEndAt = DateTime.UtcNow.AddMinutes(lockoutMinutes);
            Status = UserStatus.Locked;
        }
    }

    public bool IsLockedOut()
    {
        return LockoutEndAt.HasValue && LockoutEndAt.Value > DateTime.UtcNow;
    }

    public void Unlock()
    {
        LockoutEndAt = null;
        FailedLoginAttempts = 0;
        if (Status == UserStatus.Locked)
            Status = UserStatus.Active;
    }

    public void Activate() => Status = UserStatus.Active;

    public void Suspend() => Status = UserStatus.Suspended;

    public void Deactivate() => Status = UserStatus.Deactivated;

    public void SetRefreshToken(string refreshToken, DateTime expiresAt)
    {
        RefreshToken = refreshToken;
        RefreshTokenExpiresAt = expiresAt;
    }

    public void RevokeRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiresAt = null;
    }

    public bool ValidateRefreshToken(string refreshToken)
    {
        return RefreshToken == refreshToken &&
               RefreshTokenExpiresAt.HasValue &&
               RefreshTokenExpiresAt.Value > DateTime.UtcNow;
    }

    public void AddRole(Role role, Guid? assignedBy = null)
    {
        if (_userRoles.Any(ur => ur.RoleId == role.Id))
            return;

        _userRoles.Add(new UserRole(Id, role.Id, assignedBy));
    }

    public void RemoveRole(Guid roleId)
    {
        var userRole = _userRoles.FirstOrDefault(ur => ur.RoleId == roleId);
        if (userRole is not null)
            _userRoles.Remove(userRole);
    }

    public bool HasRole(string roleName)
    {
        return _userRoles.Any(ur =>
            ur.Role?.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase) == true);
    }

    public bool HasPermission(string permissionName)
    {
        return _userRoles.Any(ur =>
            ur.Role?.IsActive == true &&
            ur.Role.RolePermissions.Any(rp =>
                rp.Permission?.IsActive == true &&
                rp.Permission.Name.Equals(permissionName, StringComparison.OrdinalIgnoreCase)));
    }

    public IEnumerable<string> GetPermissions()
    {
        return _userRoles
            .Where(ur => ur.Role?.IsActive == true)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.Permission?.IsActive == true)
            .Select(rp => rp.Permission!.Name)
            .Distinct();
    }
}
