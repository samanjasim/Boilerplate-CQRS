using Starter.Domain.Primitives;

namespace Starter.Domain.Identity.Enums;

public sealed class UserStatus : Enumeration<UserStatus>
{
    public static readonly UserStatus Pending = new(1, nameof(Pending));
    public static readonly UserStatus Active = new(2, nameof(Active));
    public static readonly UserStatus Suspended = new(3, nameof(Suspended));
    public static readonly UserStatus Deactivated = new(4, nameof(Deactivated));
    public static readonly UserStatus Locked = new(5, nameof(Locked));

    private UserStatus(int value, string name) : base(value, name) { }

    public bool CanLogin => this == Active;
    public bool IsAccountLocked => this == Locked || this == Suspended;
}
