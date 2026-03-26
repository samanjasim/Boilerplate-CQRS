using Starter.Application.Features.Users.DTOs;
using Starter.Domain.Identity.Entities;
using Riok.Mapperly.Abstractions;

namespace Starter.Application.Features.Auth.DTOs;

[Mapper]
public static partial class UserMapper
{
    public static UserDto ToDto(
        this User user,
        bool includePermissions = false,
        string? tenantSlug = null,
        string? tenantName = null)
    {
        return new UserDto(
            user.Id,
            user.Username,
            user.Email.Value,
            user.FullName.FirstName,
            user.FullName.LastName,
            user.PhoneNumber?.Value,
            user.Status.Name,
            user.EmailConfirmed,
            user.PhoneConfirmed,
            user.LastLoginAt,
            user.CreatedAt,
            user.UserRoles
                .Where(ur => ur.Role is not null)
                .Select(ur => ur.Role!.Name)
                .ToList(),
            includePermissions ? user.GetPermissions().ToList() : null,
            user.TenantId,
            TenantName: tenantName,
            TenantSlug: tenantSlug,
            TwoFactorEnabled: user.TwoFactorEnabled);
    }

    public static IReadOnlyList<UserDto> ToDtoList(this IEnumerable<User> users)
    {
        return users.Select(u => u.ToDto()).ToList();
    }
}
