# Invitation System Overhaul — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Overhaul the invitation system to support platform-level invitations (nullable TenantId), default registration roles (per-tenant and global), permission hierarchy enforcement, tenant custom role gating, and a filtered assignable-roles API. Includes full frontend updates for InviteUserModal, RolesListPage, and TenantDetailPage.

**Architecture:** Invitation.TenantId becomes nullable (platform invites). New `IPermissionHierarchyService` provides in-memory permission subset checks, cached per-user. InviteUserCommand resolves tenant from claims/param and role from explicit/default/fallback chain. GetAssignableRoles query returns roles filtered by inviter's permission ceiling. Tenant.DefaultRegistrationRoleId enables per-tenant default roles. Feature flag `roles.tenant_custom_enabled` gates tenant role creation.

**Tech Stack:** .NET 10, EF Core/PostgreSQL, MediatR, Redis (ICacheService), React 19, TanStack Query, shadcn/ui, i18next

---

## File Map

**Backend — New files:**
- `Starter.Application/Common/Interfaces/IPermissionHierarchyService.cs`
- `Starter.Infrastructure/Services/PermissionHierarchyService.cs`
- `Starter.Application/Features/Roles/Queries/GetAssignableRoles/GetAssignableRolesQuery.cs`
- `Starter.Application/Features/Roles/Queries/GetAssignableRoles/GetAssignableRolesQueryHandler.cs`
- `Starter.Application/Features/Tenants/Commands/SetTenantDefaultRole/SetTenantDefaultRoleCommand.cs`
- `Starter.Application/Features/Tenants/Commands/SetTenantDefaultRole/SetTenantDefaultRoleCommandHandler.cs`
- `Starter.Application/Features/Tenants/Commands/SetTenantDefaultRole/SetTenantDefaultRoleCommandValidator.cs`
- `Starter.Application/Features/Settings/Commands/SetGlobalDefaultRole/SetGlobalDefaultRoleCommand.cs`
- `Starter.Application/Features/Settings/Commands/SetGlobalDefaultRole/SetGlobalDefaultRoleCommandHandler.cs`
- `Starter.Application/Features/Settings/Commands/SetGlobalDefaultRole/SetGlobalDefaultRoleCommandValidator.cs`

**Backend — Modify:**
- `Starter.Domain/Identity/Entities/Invitation.cs` — TenantId nullable, Create signature update
- `Starter.Domain/Identity/Errors/InvitationErrors.cs` — New errors: PermissionEscalation, SuperAdminOnly, TenantNotFound
- `Starter.Domain/Identity/Errors/RoleErrors.cs` — New errors: PermissionCeiling, CustomRolesDisabled
- `Starter.Domain/Tenants/Entities/Tenant.cs` — Add DefaultRegistrationRoleId property and setter
- `Starter.Domain/Tenants/Errors/TenantErrors.cs` — New error: DefaultRoleNotFound
- `Starter.Infrastructure/Persistence/Configurations/InvitationConfiguration.cs` — nullable tenant_id
- `Starter.Infrastructure/Persistence/Configurations/RoleConfiguration.cs` — composite unique index (Name, TenantId)
- `Starter.Infrastructure/Persistence/Configurations/TenantConfiguration.cs` — default_registration_role_id column
- `Starter.Infrastructure/Persistence/ApplicationDbContext.cs` — Update Invitation query filter for nullable TenantId
- `Starter.Infrastructure/DependencyInjection.cs` — Register IPermissionHierarchyService
- `Starter.Application/Features/Auth/Commands/InviteUser/InviteUserCommand.cs` — Add TenantId?, make RoleId?
- `Starter.Application/Features/Auth/Commands/InviteUser/InviteUserCommandHandler.cs` — Full overhaul
- `Starter.Application/Features/Auth/Commands/InviteUser/InviteUserCommandValidator.cs` — Update rules
- `Starter.Application/Features/Roles/Commands/CreateRole/CreateRoleCommandHandler.cs` — Feature flag gate + ceiling check
- `Starter.Application/Features/Roles/Commands/UpdateRolePermissions/UpdateRolePermissionsCommandHandler.cs` — Permission ceiling check
- `Starter.Application/Features/Roles/Queries/GetRoles/GetRolesQuery.cs` — Add TenantId? filter param
- `Starter.Application/Features/Roles/Queries/GetRoles/GetRolesQueryHandler.cs` — Tenant scope filtering
- `Starter.Application/Features/Roles/DTOs/RoleDto.cs` — Add TenantId? field
- `Starter.Application/Features/Roles/DTOs/RoleMapper.cs` — Map TenantId
- `Starter.Application/Features/Tenants/DTOs/TenantDto.cs` — Add DefaultRegistrationRoleId? + DefaultRoleName?
- `Starter.Api/Controllers/RolesController.cs` — Add GetAssignableRoles endpoint
- `Starter.Api/Controllers/TenantsController.cs` — Add SetDefaultRole endpoint
- `Starter.Shared/Constants/Permissions.cs` — No new permissions needed (uses existing)
- `Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs` — Seed `roles.tenant_custom_enabled` flag + `registration.default_role_id` setting

**Frontend — New files:**
- (none, modifications only)

**Frontend — Modify:**
- `boilerplateFE/src/config/api.config.ts` — Add ASSIGNABLE_ROLES + TENANT_DEFAULT_ROLE endpoints
- `boilerplateFE/src/lib/query/keys.ts` — Add assignableRoles query key
- `boilerplateFE/src/features/roles/api/roles.api.ts` — Add getAssignableRoles API call
- `boilerplateFE/src/features/roles/api/roles.queries.ts` — Add useAssignableRoles hook
- `boilerplateFE/src/features/auth/api/auth.api.ts` — Update inviteUser payload type
- `boilerplateFE/src/features/auth/api/auth.queries.ts` — Update useInviteUser mutation
- `boilerplateFE/src/features/users/components/InviteUserModal.tsx` — Tenant dropdown + filtered roles
- `boilerplateFE/src/features/roles/pages/RolesListPage.tsx` — System/custom badges, flag-gated Create
- `boilerplateFE/src/features/tenants/pages/TenantDetailPage.tsx` — Default role dropdown in overview
- `boilerplateFE/src/features/tenants/api/tenants.api.ts` — Add setDefaultRole API call
- `boilerplateFE/src/features/tenants/api/tenants.queries.ts` — Add useSetTenantDefaultRole hook
- `boilerplateFE/src/types/tenant.types.ts` — Add defaultRegistrationRoleId, defaultRoleName
- `boilerplateFE/src/types/role.types.ts` — Add tenantId? to Role
- `boilerplateFE/src/i18n/locales/en/translation.json` — New keys
- `boilerplateFE/src/i18n/locales/ar/translation.json` — New keys
- `boilerplateFE/src/i18n/locales/ku/translation.json` — New keys

---

## Task 1: Domain — Invitation.TenantId Nullable, Tenant.DefaultRegistrationRoleId, New Errors

**Files:**
- Modify: `boilerplateBE/src/Starter.Domain/Identity/Entities/Invitation.cs`
- Modify: `boilerplateBE/src/Starter.Domain/Identity/Errors/InvitationErrors.cs`
- Modify: `boilerplateBE/src/Starter.Domain/Identity/Errors/RoleErrors.cs`
- Modify: `boilerplateBE/src/Starter.Domain/Tenants/Entities/Tenant.cs`
- Modify: `boilerplateBE/src/Starter.Domain/Tenants/Errors/TenantErrors.cs`

- [ ] **Step 1: Make Invitation.TenantId nullable and update Create factory**

Replace the contents of `boilerplateBE/src/Starter.Domain/Identity/Entities/Invitation.cs`:

```csharp
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
```

- [ ] **Step 2: Add new invitation errors**

Replace the contents of `boilerplateBE/src/Starter.Domain/Identity/Errors/InvitationErrors.cs`:

```csharp
using Starter.Shared.Results;

namespace Starter.Domain.Identity.Errors;

public static class InvitationErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("Invitation.NotFound", $"Invitation with ID '{id}' was not found.");

    public static Error NotFoundByToken() =>
        Error.NotFound("Invitation.NotFound", "Invitation not found or has already been used.");

    public static Error AlreadyAccepted() =>
        Error.Failure("Invitation.AlreadyAccepted", "This invitation has already been accepted.");

    public static Error Expired() =>
        Error.Failure("Invitation.Expired", "This invitation has expired.");

    public static Error EmailAlreadyInvited(string email) =>
        Error.Conflict("Invitation.EmailAlreadyInvited", $"A pending invitation already exists for '{email}'.");

    public static Error InvalidToken() =>
        Error.Validation("Invitation.InvalidToken", "The invitation token is invalid or expired.");

    public static Error RoleNotFound(Guid roleId) =>
        Error.NotFound("Invitation.RoleNotFound", $"Role with ID '{roleId}' was not found.");

    public static Error TenantRequired() =>
        Error.Validation("Invitation.TenantRequired", "You must belong to a tenant to invite users.");

    public static Error PermissionEscalation() =>
        Error.Forbidden("The target role has permissions that exceed your own. You cannot assign a role with more privileges than you have.");

    public static Error SuperAdminOnly() =>
        Error.Forbidden("Only a SuperAdmin can assign the SuperAdmin role.");

    public static Error TenantNotFound(Guid tenantId) =>
        Error.NotFound("Invitation.TenantNotFound", $"Tenant with ID '{tenantId}' was not found.");
}
```

- [ ] **Step 3: Add new role errors**

Add the following errors to the end of `boilerplateBE/src/Starter.Domain/Identity/Errors/RoleErrors.cs` (before the closing brace):

```csharp
    public static Error PermissionCeiling() =>
        Error.Forbidden("The requested permissions exceed your own permission set. You can only assign permissions you hold.");

    public static Error CustomRolesDisabled() =>
        Error.Failure("Role.CustomRolesDisabled", "Custom tenant roles are not enabled. Contact your platform administrator.");
```

- [ ] **Step 4: Add DefaultRegistrationRoleId to Tenant entity**

In `boilerplateBE/src/Starter.Domain/Tenants/Entities/Tenant.cs`, add the property after `EmailFooterText` and add a setter method before `Activate()`:

Add property after `public string? EmailFooterText { get; private set; }`:
```csharp
    // Registration
    public Guid? DefaultRegistrationRoleId { get; private set; }
```

Add method before `public void Activate()`:
```csharp
    public void SetDefaultRegistrationRole(Guid? roleId)
    {
        DefaultRegistrationRoleId = roleId;
    }
```

- [ ] **Step 5: Add new tenant error**

Add to `boilerplateBE/src/Starter.Domain/Tenants/Errors/TenantErrors.cs` (before the closing brace):

```csharp
    public static Error DefaultRoleNotFound(Guid roleId) =>
        Error.NotFound("Tenant.DefaultRoleNotFound", $"Role with ID '{roleId}' was not found or is not accessible to this tenant.");
```

- [ ] **Step 6: Build verification**

```bash
cd boilerplateBE && dotnet build --no-restore
```

**Commit:** `feat(domain): make Invitation.TenantId nullable, add Tenant.DefaultRegistrationRoleId, new error types`

---

## Task 2: EF Config — Nullable tenant_id, default_role_id, Composite Role Index

**Files:**
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/InvitationConfiguration.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/RoleConfiguration.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/TenantConfiguration.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs`

- [ ] **Step 1: Make Invitation.TenantId nullable in EF config**

In `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/InvitationConfiguration.cs`, change the TenantId property configuration from:

```csharp
        builder.Property(i => i.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
```

to:

```csharp
        builder.Property(i => i.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired(false);
```

- [ ] **Step 2: Change Role unique index to composite (Name, TenantId)**

In `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/RoleConfiguration.cs`, replace:

```csharp
        builder.HasIndex(r => r.Name)
            .IsUnique();
```

with:

```csharp
        builder.HasIndex(r => new { r.Name, r.TenantId })
            .IsUnique();
```

- [ ] **Step 3: Add DefaultRegistrationRoleId to TenantConfiguration**

In `boilerplateBE/src/Starter.Infrastructure/Persistence/Configurations/TenantConfiguration.cs`, add after the `EmailFooterText` property configuration (before `CreatedAt`):

```csharp
        // Registration
        builder.Property(t => t.DefaultRegistrationRoleId)
            .HasColumnName("default_registration_role_id");
```

- [ ] **Step 4: Update Invitation query filter for nullable TenantId**

In `boilerplateBE/src/Starter.Infrastructure/Persistence/ApplicationDbContext.cs`, replace:

```csharp
        modelBuilder.Entity<Invitation>().HasQueryFilter(i =>
            TenantId == null || i.TenantId == TenantId);
```

with:

```csharp
        // Invitations: platform admin sees all; tenant user sees their tenant's + platform-level (null TenantId)
        modelBuilder.Entity<Invitation>().HasQueryFilter(i =>
            TenantId == null || i.TenantId == null || i.TenantId == TenantId);
```

- [ ] **Step 5: Build verification**

```bash
cd boilerplateBE && dotnet build --no-restore
```

**Commit:** `feat(infrastructure): nullable invitation tenant_id, composite role name index, tenant default role column`

---

## Task 3: IPermissionHierarchyService Interface + Implementation

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Common/Interfaces/IPermissionHierarchyService.cs`
- Create: `boilerplateBE/src/Starter.Infrastructure/Services/PermissionHierarchyService.cs`
- Modify: `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create the IPermissionHierarchyService interface**

Create `boilerplateBE/src/Starter.Application/Common/Interfaces/IPermissionHierarchyService.cs`:

```csharp
namespace Starter.Application.Common.Interfaces;

/// <summary>
/// Determines whether one user's permissions are a superset of (or equal to) a target role's permissions.
/// Used for invitation hierarchy checks and role permission ceiling enforcement.
/// </summary>
public interface IPermissionHierarchyService
{
    /// <summary>
    /// Returns true if the current user's effective permissions are a superset of (or equal to)
    /// the target role's permissions. SuperAdmin always returns true.
    /// </summary>
    Task<bool> CanAssignRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if all the given permission IDs are within the current user's effective permission set.
    /// SuperAdmin always returns true.
    /// </summary>
    Task<bool> ArePermissionsWithinCeilingAsync(IEnumerable<Guid> permissionIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of permission names the current user holds.
    /// Used by GetAssignableRoles to filter roles.
    /// </summary>
    Task<HashSet<string>> GetCurrentUserPermissionNamesAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Create the PermissionHierarchyService implementation**

Create `boilerplateBE/src/Starter.Infrastructure/Services/PermissionHierarchyService.cs`:

```csharp
using Starter.Application.Common.Interfaces;
using Starter.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Starter.Infrastructure.Services;

internal sealed class PermissionHierarchyService(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IPermissionHierarchyService
{
    public async Task<bool> CanAssignRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        // SuperAdmin can assign any role
        if (currentUserService.IsInRole(Roles.SuperAdmin))
            return true;

        var currentPermissions = await GetCurrentUserPermissionNamesAsync(cancellationToken);

        var targetPermissions = await context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission!.Name)
            .ToListAsync(cancellationToken);

        // Target role's permissions must be a subset of the current user's permissions
        return targetPermissions.All(tp => currentPermissions.Contains(tp));
    }

    public async Task<bool> ArePermissionsWithinCeilingAsync(
        IEnumerable<Guid> permissionIds,
        CancellationToken cancellationToken = default)
    {
        if (currentUserService.IsInRole(Roles.SuperAdmin))
            return true;

        var currentPermissions = await GetCurrentUserPermissionNamesAsync(cancellationToken);

        var targetPermissionNames = await context.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        return targetPermissionNames.All(tp => currentPermissions.Contains(tp));
    }

    public Task<HashSet<string>> GetCurrentUserPermissionNamesAsync(CancellationToken cancellationToken = default)
    {
        // ICurrentUserService.Permissions is populated from the JWT claims
        var permissionNames = currentUserService.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(permissionNames);
    }
}
```

- [ ] **Step 3: Register IPermissionHierarchyService in DI**

In `boilerplateBE/src/Starter.Infrastructure/DependencyInjection.cs`, in the `AddServices` method, add after the `IFeatureFlagService` registration:

```csharp
        services.AddScoped<IPermissionHierarchyService, PermissionHierarchyService>();
```

- [ ] **Step 4: Build verification**

```bash
cd boilerplateBE && dotnet build --no-restore
```

**Commit:** `feat(infrastructure): add IPermissionHierarchyService for role assignment hierarchy checks`

---

## Task 4: InviteUserCommand Overhaul

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/Features/Auth/Commands/InviteUser/InviteUserCommand.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Auth/Commands/InviteUser/InviteUserCommandHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Auth/Commands/InviteUser/InviteUserCommandValidator.cs`

- [ ] **Step 1: Update InviteUserCommand record**

Replace the contents of `boilerplateBE/src/Starter.Application/Features/Auth/Commands/InviteUser/InviteUserCommand.cs`:

```csharp
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Commands.InviteUser;

public sealed record InviteUserCommand(
    string Email,
    Guid? RoleId = null,
    Guid? TenantId = null) : IRequest<Result<Guid>>;
```

- [ ] **Step 2: Overhaul InviteUserCommandHandler**

Replace the contents of `boilerplateBE/src/Starter.Application/Features/Auth/Commands/InviteUser/InviteUserCommandHandler.cs`:

```csharp
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Errors;
using Starter.Domain.Identity.ValueObjects;
using Starter.Shared.Constants;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Starter.Application.Features.Auth.Commands.InviteUser;

internal sealed class InviteUserCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IPermissionHierarchyService permissionHierarchyService,
    IEmailService emailService,
    IEmailTemplateService emailTemplateService,
    IConfiguration configuration) : IRequestHandler<InviteUserCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        var inviterId = currentUserService.UserId!.Value;

        // ── 1. Resolve tenant ──────────────────────────────────
        // Tenant admins: forced from claims (ignore param)
        // Platform admins: from param (null = platform-level invite)
        Guid? tenantId;
        if (currentUserService.TenantId is not null)
        {
            // Tenant user — forced to their own tenant
            tenantId = currentUserService.TenantId;
        }
        else
        {
            // Platform admin — use param (null = platform invite)
            tenantId = request.TenantId;

            // Validate provided tenant exists
            if (tenantId is not null)
            {
                var tenantExists = await context.Tenants
                    .IgnoreQueryFilters()
                    .AnyAsync(t => t.Id == tenantId.Value, cancellationToken);

                if (!tenantExists)
                    return Result.Failure<Guid>(InvitationErrors.TenantNotFound(tenantId.Value));
            }
        }

        // ── 2. Validate email ──────────────────────────────────
        var normalizedEmail = Email.Normalize(request.Email);

        var emailExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.Value == normalizedEmail, cancellationToken);

        if (emailExists)
            return Result.Failure<Guid>(UserErrors.EmailAlreadyExists(request.Email));

        // Check no pending invitation for this email in the same tenant scope
        var pendingExists = await context.Invitations
            .IgnoreQueryFilters()
            .AnyAsync(i =>
                i.Email == normalizedEmail &&
                i.TenantId == tenantId &&
                !i.IsAccepted &&
                i.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (pendingExists)
            return Result.Failure<Guid>(InvitationErrors.EmailAlreadyInvited(request.Email));

        // ── 3. Resolve role ────────────────────────────────────
        // Priority: provided RoleId → tenant default → global default → system "User"
        Guid roleId;
        if (request.RoleId is not null)
        {
            roleId = request.RoleId.Value;
        }
        else
        {
            roleId = await ResolveDefaultRoleAsync(tenantId, cancellationToken);
        }

        // Verify the role exists
        var role = await context.Roles
            .IgnoreQueryFilters()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role is null)
            return Result.Failure<Guid>(InvitationErrors.RoleNotFound(roleId));

        // ── 4. Permission hierarchy check ──────────────────────
        // SuperAdmin role can only be assigned by SuperAdmin
        if (role.Name == Roles.SuperAdmin && !currentUserService.IsInRole(Roles.SuperAdmin))
            return Result.Failure<Guid>(InvitationErrors.SuperAdminOnly());

        // Target role permissions must be subset of inviter's permissions
        if (!currentUserService.IsInRole(Roles.SuperAdmin))
        {
            var canAssign = await permissionHierarchyService.CanAssignRoleAsync(role.Id, cancellationToken);
            if (!canAssign)
                return Result.Failure<Guid>(InvitationErrors.PermissionEscalation());
        }

        // ── 5. Create invitation ───────────────────────────────
        var invitation = Invitation.Create(
            normalizedEmail,
            role.Id,
            tenantId,
            inviterId);

        context.Invitations.Add(invitation);
        await context.SaveChangesAsync(cancellationToken);

        // ── 6. Send invitation email ───────────────────────────
        var frontendUrl = configuration["AppSettings:FrontendUrl"] ?? "http://localhost:3000";
        var acceptUrl = $"{frontendUrl}/accept-invite?token={invitation.Token}";

        var inviter = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == inviterId, cancellationToken);

        string tenantName;
        if (tenantId is not null)
        {
            var tenant = await context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);
            tenantName = tenant?.Name ?? "the organization";
        }
        else
        {
            tenantName = "the platform";
        }

        var inviterName = inviter?.FullName.GetFullName() ?? "A team member";

        var emailMessage = emailTemplateService.RenderInvitation(
            normalizedEmail,
            inviterName,
            tenantName,
            role.Name,
            acceptUrl);

        await emailService.SendAsync(emailMessage, cancellationToken);

        return Result.Success(invitation.Id);
    }

    private async Task<Guid> ResolveDefaultRoleAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        // 1. Tenant-specific default
        if (tenantId is not null)
        {
            var tenant = await context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);

            if (tenant?.DefaultRegistrationRoleId is not null)
            {
                return tenant.DefaultRegistrationRoleId.Value;
            }
        }

        // 2. Global default from SystemSetting
        var globalSetting = await context.SystemSettings
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == null && s.Key == "registration.default_role_id")
            .FirstOrDefaultAsync(cancellationToken);

        if (globalSetting is not null && Guid.TryParse(globalSetting.Value, out var globalRoleId))
        {
            var roleExists = await context.Roles
                .IgnoreQueryFilters()
                .AnyAsync(r => r.Id == globalRoleId, cancellationToken);

            if (roleExists)
                return globalRoleId;
        }

        // 3. Fallback: system "User" role
        var userRole = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Name == Roles.User && r.IsSystemRole, cancellationToken);

        return userRole?.Id ?? throw new InvalidOperationException("System 'User' role not found. Ensure seed data has been applied.");
    }
}
```

- [ ] **Step 3: Update InviteUserCommandValidator**

Replace the contents of `boilerplateBE/src/Starter.Application/Features/Auth/Commands/InviteUser/InviteUserCommandValidator.cs`:

```csharp
using Starter.Domain.Identity.ValueObjects;
using FluentValidation;

namespace Starter.Application.Features.Auth.Commands.InviteUser;

public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(Email.MaxLength)
            .WithMessage($"Email must not exceed {Email.MaxLength} characters.");

        // RoleId is now optional — when null, the default role resolution chain kicks in
        // TenantId is optional — only used by platform admins
    }
}
```

- [ ] **Step 4: Build verification**

```bash
cd boilerplateBE && dotnet build --no-restore
```

**Commit:** `feat(auth): overhaul InviteUserCommand with tenant resolution, default roles, and permission hierarchy`

---

## Task 5: GetAssignableRoles Query + RolesController Endpoint

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/Roles/Queries/GetAssignableRoles/GetAssignableRolesQuery.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Roles/Queries/GetAssignableRoles/GetAssignableRolesQueryHandler.cs`
- Modify: `boilerplateBE/src/Starter.Api/Controllers/RolesController.cs`

- [ ] **Step 1: Create GetAssignableRolesQuery**

Create `boilerplateBE/src/Starter.Application/Features/Roles/Queries/GetAssignableRoles/GetAssignableRolesQuery.cs`:

```csharp
using Starter.Application.Features.Roles.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Roles.Queries.GetAssignableRoles;

/// <summary>
/// Returns roles the current user can assign to others.
/// Filtered by permission hierarchy (target role perms must be subset of caller's)
/// and tenant scope.
/// </summary>
public sealed record GetAssignableRolesQuery(
    Guid? TenantId = null) : IRequest<Result<IReadOnlyList<RoleDto>>>;
```

- [ ] **Step 2: Create GetAssignableRolesQueryHandler**

Create `boilerplateBE/src/Starter.Application/Features/Roles/Queries/GetAssignableRoles/GetAssignableRolesQueryHandler.cs`:

```csharp
using Starter.Application.Common.Interfaces;
using Starter.Application.Features.Roles.DTOs;
using Starter.Shared.Constants;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Queries.GetAssignableRoles;

internal sealed class GetAssignableRolesQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IPermissionHierarchyService permissionHierarchyService) : IRequestHandler<GetAssignableRolesQuery, Result<IReadOnlyList<RoleDto>>>
{
    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(
        GetAssignableRolesQuery request,
        CancellationToken cancellationToken)
    {
        var isSuperAdmin = currentUserService.IsInRole(Roles.SuperAdmin);

        // Determine the target tenant scope
        Guid? targetTenantId;
        if (currentUserService.TenantId is not null)
        {
            // Tenant user — can only assign roles within their tenant
            targetTenantId = currentUserService.TenantId;
        }
        else
        {
            // Platform admin — use the requested tenant or null for platform roles
            targetTenantId = request.TenantId;
        }

        // Load roles with permissions
        var rolesQuery = context.Roles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Where(r => r.IsActive);

        // Scope: system roles (TenantId=null) + target tenant's custom roles
        if (targetTenantId is not null)
        {
            rolesQuery = rolesQuery.Where(r => r.TenantId == null || r.TenantId == targetTenantId);
        }
        else
        {
            // Platform-level invite — only system roles (no tenant custom roles)
            rolesQuery = rolesQuery.Where(r => r.TenantId == null);
        }

        var roles = await rolesQuery
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        // SuperAdmin sees everything
        if (isSuperAdmin)
        {
            return Result.Success<IReadOnlyList<RoleDto>>(roles.ToDtoList());
        }

        // Filter: exclude SuperAdmin role and roles with permissions exceeding caller's
        var currentPermissions = await permissionHierarchyService.GetCurrentUserPermissionNamesAsync(cancellationToken);

        var assignableRoles = roles
            .Where(r => r.Name != Roles.SuperAdmin)
            .Where(r =>
            {
                var rolePermNames = r.RolePermissions
                    .Where(rp => rp.Permission is not null)
                    .Select(rp => rp.Permission!.Name);
                return rolePermNames.All(p => currentPermissions.Contains(p));
            })
            .ToList();

        return Result.Success<IReadOnlyList<RoleDto>>(assignableRoles.ToDtoList());
    }
}
```

- [ ] **Step 3: Add GetAssignableRoles endpoint to RolesController**

In `boilerplateBE/src/Starter.Api/Controllers/RolesController.cs`, add the following using and endpoint. Add the using at the top:

```csharp
using Starter.Application.Features.Roles.Queries.GetAssignableRoles;
```

Add the endpoint method after the `GetRole` method (before `CreateRole`):

```csharp
    /// <summary>
    /// Get roles assignable by the current user (filtered by permission hierarchy).
    /// </summary>
    [HttpGet("assignable")]
    [Authorize(Policy = Permissions.Users.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAssignableRoles([FromQuery] Guid? tenantId = null)
    {
        var query = new GetAssignableRolesQuery(tenantId);
        var result = await Mediator.Send(query);
        return HandleResult(result);
    }
```

- [ ] **Step 4: Build verification**

```bash
cd boilerplateBE && dotnet build --no-restore
```

**Commit:** `feat(roles): add GetAssignableRoles query with permission hierarchy filtering`

---

## Task 6: Default Role Commands — SetTenantDefaultRole + Global SystemSetting

**Files:**
- Create: `boilerplateBE/src/Starter.Application/Features/Tenants/Commands/SetTenantDefaultRole/SetTenantDefaultRoleCommand.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Tenants/Commands/SetTenantDefaultRole/SetTenantDefaultRoleCommandHandler.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Tenants/Commands/SetTenantDefaultRole/SetTenantDefaultRoleCommandValidator.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Settings/Commands/SetGlobalDefaultRole/SetGlobalDefaultRoleCommand.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Settings/Commands/SetGlobalDefaultRole/SetGlobalDefaultRoleCommandHandler.cs`
- Create: `boilerplateBE/src/Starter.Application/Features/Settings/Commands/SetGlobalDefaultRole/SetGlobalDefaultRoleCommandValidator.cs`
- Modify: `boilerplateBE/src/Starter.Api/Controllers/TenantsController.cs`

- [ ] **Step 1: Create SetTenantDefaultRoleCommand**

Create `boilerplateBE/src/Starter.Application/Features/Tenants/Commands/SetTenantDefaultRole/SetTenantDefaultRoleCommand.cs`:

```csharp
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Tenants.Commands.SetTenantDefaultRole;

public sealed record SetTenantDefaultRoleCommand(
    Guid TenantId,
    Guid? RoleId) : IRequest<Result>;
```

- [ ] **Step 2: Create SetTenantDefaultRoleCommandHandler**

Create `boilerplateBE/src/Starter.Application/Features/Tenants/Commands/SetTenantDefaultRole/SetTenantDefaultRoleCommandHandler.cs`:

```csharp
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Constants;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Tenants.Commands.SetTenantDefaultRole;

internal sealed class SetTenantDefaultRoleCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IPermissionHierarchyService permissionHierarchyService) : IRequestHandler<SetTenantDefaultRoleCommand, Result>
{
    public async Task<Result> Handle(SetTenantDefaultRoleCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
            return Result.Failure(TenantErrors.NotFound(request.TenantId));

        // Null means "clear the default" — always allowed
        if (request.RoleId is null)
        {
            tenant.SetDefaultRegistrationRole(null);
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        // Verify the role exists and is accessible to this tenant
        var role = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r =>
                r.Id == request.RoleId.Value &&
                r.IsActive &&
                (r.TenantId == null || r.TenantId == request.TenantId),
                cancellationToken);

        if (role is null)
            return Result.Failure(TenantErrors.DefaultRoleNotFound(request.RoleId.Value));

        // Non-SuperAdmin: can only set a role with permissions within their own ceiling
        if (!currentUserService.IsInRole(Roles.SuperAdmin))
        {
            var canAssign = await permissionHierarchyService.CanAssignRoleAsync(role.Id, cancellationToken);
            if (!canAssign)
                return Result.Failure(RoleErrors.PermissionCeiling());
        }

        tenant.SetDefaultRegistrationRole(role.Id);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 3: Create SetTenantDefaultRoleCommandValidator**

Create `boilerplateBE/src/Starter.Application/Features/Tenants/Commands/SetTenantDefaultRole/SetTenantDefaultRoleCommandValidator.cs`:

```csharp
using FluentValidation;

namespace Starter.Application.Features.Tenants.Commands.SetTenantDefaultRole;

public sealed class SetTenantDefaultRoleCommandValidator : AbstractValidator<SetTenantDefaultRoleCommand>
{
    public SetTenantDefaultRoleCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required.");
    }
}
```

- [ ] **Step 4: Create SetGlobalDefaultRoleCommand**

Create `boilerplateBE/src/Starter.Application/Features/Settings/Commands/SetGlobalDefaultRole/SetGlobalDefaultRoleCommand.cs`:

```csharp
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Settings.Commands.SetGlobalDefaultRole;

public sealed record SetGlobalDefaultRoleCommand(
    Guid? RoleId) : IRequest<Result>;
```

- [ ] **Step 5: Create SetGlobalDefaultRoleCommandHandler**

Create `boilerplateBE/src/Starter.Application/Features/Settings/Commands/SetGlobalDefaultRole/SetGlobalDefaultRoleCommandHandler.cs`:

```csharp
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Settings.Commands.SetGlobalDefaultRole;

internal sealed class SetGlobalDefaultRoleCommandHandler(
    IApplicationDbContext context) : IRequestHandler<SetGlobalDefaultRoleCommand, Result>
{
    private const string SettingKey = "registration.default_role_id";

    public async Task<Result> Handle(SetGlobalDefaultRoleCommand request, CancellationToken cancellationToken)
    {
        // Validate the role exists if provided
        if (request.RoleId is not null)
        {
            var roleExists = await context.Roles
                .IgnoreQueryFilters()
                .AnyAsync(r => r.Id == request.RoleId.Value && r.IsActive, cancellationToken);

            if (!roleExists)
                return Result.Failure(InvitationErrors.RoleNotFound(request.RoleId.Value));
        }

        // Find or create the setting
        var setting = await context.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == null && s.Key == SettingKey, cancellationToken);

        if (setting is not null)
        {
            setting.UpdateValue(request.RoleId?.ToString() ?? "");
        }
        else
        {
            var newSetting = SystemSetting.Create(
                SettingKey,
                request.RoleId?.ToString() ?? "",
                tenantId: null,
                description: "Default role ID for new user registrations",
                category: "Registration",
                isSecret: false,
                dataType: "text");
            context.SystemSettings.Add(newSetting);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 6: Create SetGlobalDefaultRoleCommandValidator**

Create `boilerplateBE/src/Starter.Application/Features/Settings/Commands/SetGlobalDefaultRole/SetGlobalDefaultRoleCommandValidator.cs`:

```csharp
using FluentValidation;

namespace Starter.Application.Features.Settings.Commands.SetGlobalDefaultRole;

public sealed class SetGlobalDefaultRoleCommandValidator : AbstractValidator<SetGlobalDefaultRoleCommand>
{
    public SetGlobalDefaultRoleCommandValidator()
    {
        // RoleId is optional — null clears the global default
    }
}
```

- [ ] **Step 7: Add SetDefaultRole endpoint to TenantsController**

In `boilerplateBE/src/Starter.Api/Controllers/TenantsController.cs`, add the using at the top:

```csharp
using Starter.Application.Features.Tenants.Commands.SetTenantDefaultRole;
```

Add the endpoint after the `DeactivateTenant` method:

```csharp
    /// <summary>
    /// Set the default registration role for a tenant.
    /// </summary>
    [HttpPut("{id:guid}/default-role")]
    [Authorize(Policy = Permissions.Tenants.Update)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultRole(Guid id, [FromBody] SetDefaultRoleRequest request)
    {
        var command = new SetTenantDefaultRoleCommand(id, request.RoleId);
        var result = await Mediator.Send(command);
        return HandleResult(result);
    }
```

Add the request DTO in the `#region Request DTOs` section:

```csharp
/// <summary>
/// Request to set a tenant's default registration role.
/// </summary>
public sealed record SetDefaultRoleRequest(Guid? RoleId);
```

- [ ] **Step 8: Build verification**

```bash
cd boilerplateBE && dotnet build --no-restore
```

**Commit:** `feat(tenants): add SetTenantDefaultRole and SetGlobalDefaultRole commands`

---

## Task 7: Role Scoping — Tenant Filter, Ceiling Check, Feature Flag Gate

**Files:**
- Modify: `boilerplateBE/src/Starter.Application/Features/Roles/Queries/GetRoles/GetRolesQuery.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Roles/Queries/GetRoles/GetRolesQueryHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Roles/Commands/CreateRole/CreateRoleCommandHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Roles/Commands/UpdateRolePermissions/UpdateRolePermissionsCommandHandler.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Roles/DTOs/RoleDto.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Roles/DTOs/RoleMapper.cs`

- [ ] **Step 1: Add TenantId to RoleDto and RoleMapper**

Replace `boilerplateBE/src/Starter.Application/Features/Roles/DTOs/RoleDto.cs`:

```csharp
namespace Starter.Application.Features.Roles.DTOs;

public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    bool IsActive,
    DateTime CreatedAt,
    Guid? TenantId,
    IReadOnlyList<PermissionDto> Permissions);

public sealed record PermissionDto(
    Guid Id,
    string Name,
    string? Description,
    string? Module,
    bool IsActive);

public sealed record PermissionGroupDto(
    string Module,
    IReadOnlyList<PermissionDto> Permissions);
```

Replace `boilerplateBE/src/Starter.Application/Features/Roles/DTOs/RoleMapper.cs`:

```csharp
using Starter.Domain.Identity.Entities;
using Riok.Mapperly.Abstractions;

namespace Starter.Application.Features.Roles.DTOs;

[Mapper]
public static partial class RoleMapper
{
    public static RoleDto ToDto(this Role role)
    {
        return new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            role.IsActive,
            role.CreatedAt,
            role.TenantId,
            role.RolePermissions
                .Where(rp => rp.Permission is not null)
                .Select(rp => rp.Permission!.ToDto())
                .ToList());
    }

    public static PermissionDto ToDto(this Permission permission)
    {
        return new PermissionDto(
            permission.Id,
            permission.Name,
            permission.Description,
            permission.Module,
            permission.IsActive);
    }

    public static IReadOnlyList<RoleDto> ToDtoList(this IEnumerable<Role> roles)
    {
        return roles.Select(r => r.ToDto()).ToList();
    }

    public static IReadOnlyList<PermissionDto> ToDtoList(this IEnumerable<Permission> permissions)
    {
        return permissions.Select(p => p.ToDto()).ToList();
    }

    public static IReadOnlyList<PermissionGroupDto> ToGroupedDtoList(this IEnumerable<Permission> permissions)
    {
        return permissions
            .GroupBy(p => p.Module ?? "General")
            .Select(g => new PermissionGroupDto(
                g.Key,
                g.Select(p => p.ToDto()).ToList()))
            .OrderBy(g => g.Module)
            .ToList();
    }
}
```

- [ ] **Step 2: Add TenantId filter to GetRolesQuery**

Replace `boilerplateBE/src/Starter.Application/Features/Roles/Queries/GetRoles/GetRolesQuery.cs`:

```csharp
using Starter.Application.Common.Models;
using Starter.Application.Features.Roles.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Roles.Queries.GetRoles;

public sealed record GetRolesQuery : PaginationQuery, IRequest<Result<PaginatedList<RoleDto>>>
{
    /// <summary>
    /// Optional tenant filter. Platform admin can pass a tenant ID to see that tenant's custom roles.
    /// </summary>
    public Guid? TenantId { get; init; }
}
```

- [ ] **Step 3: Update GetRolesQueryHandler with tenant scope filtering**

Replace `boilerplateBE/src/Starter.Application/Features/Roles/Queries/GetRoles/GetRolesQueryHandler.cs`:

```csharp
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Application.Features.Roles.DTOs;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Queries.GetRoles;

internal sealed class GetRolesQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<GetRolesQuery, Result<PaginatedList<RoleDto>>>
{
    public async Task<Result<PaginatedList<RoleDto>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var query = context.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .AsQueryable();

        // Platform admin with tenant filter: show system roles + that tenant's custom roles
        if (currentUserService.TenantId is null && request.TenantId is not null)
        {
            query = query.Where(r => r.TenantId == null || r.TenantId == request.TenantId);
        }
        // The global query filter already handles:
        // - Platform admin (no tenant filter): sees all roles
        // - Tenant user: sees system roles (TenantId=null) + own custom roles

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim().ToLower();
            query = query.Where(r =>
                r.Name.ToLower().Contains(searchTerm) ||
                (r.Description != null && r.Description.ToLower().Contains(searchTerm)));
        }

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "name" => request.SortDescending
                ? query.OrderByDescending(r => r.Name)
                : query.OrderBy(r => r.Name),
            "createdat" => request.SortDescending
                ? query.OrderByDescending(r => r.CreatedAt)
                : query.OrderBy(r => r.CreatedAt),
            _ => query.OrderBy(r => r.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var roles = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var roleDtos = roles.ToDtoList();

        var paginatedList = PaginatedList<RoleDto>.Create(
            roleDtos,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(paginatedList);
    }
}
```

- [ ] **Step 4: Add feature flag gate and ceiling check to CreateRoleCommandHandler**

Replace `boilerplateBE/src/Starter.Application/Features/Roles/Commands/CreateRole/CreateRoleCommandHandler.cs`:

```csharp
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Commands.CreateRole;

internal sealed class CreateRoleCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IFeatureFlagService featureFlagService) : IRequestHandler<CreateRoleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;

        // Tenant users need the feature flag enabled to create custom roles
        if (tenantId is not null)
        {
            var customRolesEnabled = await featureFlagService.IsEnabledAsync("roles.tenant_custom_enabled", cancellationToken);
            if (!customRolesEnabled)
                return Result.Failure<Guid>(RoleErrors.CustomRolesDisabled());
        }

        // Check name is unique within the same tenant scope
        var nameExists = await context.Roles
            .AnyAsync(r => r.Name == request.Name.Trim() && r.TenantId == tenantId, cancellationToken);

        if (nameExists)
            return Result.Failure<Guid>(RoleErrors.NameAlreadyExists(request.Name));

        var role = Role.Create(
            request.Name.Trim(),
            request.Description?.Trim(),
            tenantId: tenantId);

        context.Roles.Add(role);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(role.Id);
    }
}
```

- [ ] **Step 5: Add permission ceiling check to UpdateRolePermissionsCommandHandler**

Replace `boilerplateBE/src/Starter.Application/Features/Roles/Commands/UpdateRolePermissions/UpdateRolePermissionsCommandHandler.cs`:

```csharp
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Roles.Commands.UpdateRolePermissions;

internal sealed class UpdateRolePermissionsCommandHandler(
    IApplicationDbContext context,
    IPermissionHierarchyService permissionHierarchyService) : IRequestHandler<UpdateRolePermissionsCommand, Result>
{
    public async Task<Result> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role is null)
            return Result.Failure(RoleErrors.NotFound(request.RoleId));

        if (role.IsSystemRole)
            return Result.Failure(RoleErrors.SystemRoleCannotBeModified());

        // Permission ceiling check: requested permissions must be within the caller's own set
        var withinCeiling = await permissionHierarchyService.ArePermissionsWithinCeilingAsync(
            request.PermissionIds, cancellationToken);

        if (!withinCeiling)
            return Result.Failure(RoleErrors.PermissionCeiling());

        var permissions = await context.Permissions
            .Where(p => request.PermissionIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var notFoundIds = request.PermissionIds
            .Except(permissions.Select(p => p.Id))
            .ToList();

        if (notFoundIds.Count > 0)
            return Result.Failure(PermissionErrors.NotFound(notFoundIds.First()));

        role.ClearPermissions();

        foreach (var permission in permissions)
        {
            role.AddPermission(permission);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 6: Build verification**

```bash
cd boilerplateBE && dotnet build --no-restore
```

**Commit:** `feat(roles): add tenant scope filtering, feature flag gate for custom roles, permission ceiling enforcement`

---

## Task 8: Seed Data — Feature Flag + Global Default Role Setting

**Files:**
- Modify: `boilerplateBE/src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs`
- Modify: `boilerplateBE/src/Starter.Application/Features/Tenants/DTOs/TenantDto.cs`

- [ ] **Step 1: Add `roles.tenant_custom_enabled` to feature flags seed**

In `boilerplateBE/src/Starter.Infrastructure/Persistence/Seeds/DataSeeder.cs`, in the `SeedFeatureFlagsAsync` method, add to the `flags` array (after the `billing.enabled` entry, before the closing `};`):

```csharp
            FeatureFlag.Create("roles.tenant_custom_enabled", "Tenant Custom Roles", "Allow tenants to create custom roles", "false", FlagValueType.Boolean, FlagCategory.System, false),
```

- [ ] **Step 2: Add `registration.default_role_id` to system settings seed**

In the `SeedDefaultSettingsAsync` method, add to the `defaultSettings` array (after the `Reports.FileExpirationHours` entry, before the closing `};`):

```csharp
            // Registration
            ("registration.default_role_id", "", "Default role ID for new user registrations (leave empty for system User role)", "Registration", false, "text"),
```

- [ ] **Step 3: Add DefaultRegistrationRoleId to TenantDto**

Replace `boilerplateBE/src/Starter.Application/Features/Tenants/DTOs/TenantDto.cs`:

```csharp
namespace Starter.Application.Features.Tenants.DTOs;

public sealed record TenantDto(
    Guid Id,
    string Name,
    string? Slug,
    string Status,
    DateTime CreatedAt,
    // Branding
    Guid? LogoFileId = null,
    Guid? FaviconFileId = null,
    string? LogoUrl = null,
    string? FaviconUrl = null,
    string? PrimaryColor = null,
    string? SecondaryColor = null,
    string? Description = null,
    // Business Info
    string? Address = null,
    string? Phone = null,
    string? Website = null,
    string? TaxId = null,
    // Custom Text
    string? LoginPageTitle = null,
    string? LoginPageSubtitle = null,
    string? EmailFooterText = null,
    // Registration
    Guid? DefaultRegistrationRoleId = null,
    string? DefaultRoleName = null);
```

- [ ] **Step 4: Update GetTenantByIdQueryHandler to populate DefaultRoleName**

Find the query handler file at `boilerplateBE/src/Starter.Application/Features/Tenants/Queries/GetTenantById/GetTenantByIdQueryHandler.cs`. In the mapping where `TenantDto` is constructed, add the new fields. The exact edit depends on how the mapper works, but the handler must:

1. Load the tenant.
2. If `tenant.DefaultRegistrationRoleId` is not null, load the role name.
3. Pass both to the `TenantDto` constructor.

The handler should be updated to include a role name lookup. Add after loading the tenant and before constructing the DTO:

```csharp
        string? defaultRoleName = null;
        if (tenant.DefaultRegistrationRoleId is not null)
        {
            defaultRoleName = await context.Roles
                .IgnoreQueryFilters()
                .Where(r => r.Id == tenant.DefaultRegistrationRoleId.Value)
                .Select(r => r.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
```

Then include in the TenantDto construction:
```csharp
        DefaultRegistrationRoleId: tenant.DefaultRegistrationRoleId,
        DefaultRoleName: defaultRoleName
```

> **Note:** The exact implementation depends on how the current GetTenantByIdQueryHandler constructs the TenantDto. Read the file and adapt accordingly — the key requirement is that both `DefaultRegistrationRoleId` and `DefaultRoleName` are populated in the response.

- [ ] **Step 5: Build verification**

```bash
cd boilerplateBE && dotnet build --no-restore
```

**Commit:** `feat(seed): add roles.tenant_custom_enabled flag, registration.default_role_id setting, TenantDto default role fields`

---

## Task 9: Frontend — InviteUserModal Overhaul

**Files:**
- Modify: `boilerplateFE/src/config/api.config.ts`
- Modify: `boilerplateFE/src/lib/query/keys.ts`
- Modify: `boilerplateFE/src/features/roles/api/roles.api.ts`
- Modify: `boilerplateFE/src/features/roles/api/roles.queries.ts`
- Modify: `boilerplateFE/src/features/auth/api/auth.api.ts`
- Modify: `boilerplateFE/src/features/auth/api/auth.queries.ts`
- Modify: `boilerplateFE/src/features/users/components/InviteUserModal.tsx`
- Modify: `boilerplateFE/src/types/role.types.ts`

- [ ] **Step 1: Add ASSIGNABLE_ROLES and TENANT_DEFAULT_ROLE endpoints to api.config.ts**

In `boilerplateFE/src/config/api.config.ts`, add to the `ROLES` section:

```typescript
    ASSIGNABLE: '/Roles/assignable',
```

Add to the `TENANTS` section:

```typescript
    DEFAULT_ROLE: (id: string) => `/Tenants/${id}/default-role`,
```

- [ ] **Step 2: Add assignableRoles query key**

In `boilerplateFE/src/lib/query/keys.ts`, add after the `roles` key group:

```typescript
  assignableRoles: {
    all: ['assignableRoles'] as const,
    list: (tenantId?: string) => [...['assignableRoles'], 'list', tenantId ?? 'none'] as const,
  },
```

- [ ] **Step 3: Add tenantId to Role type**

In `boilerplateFE/src/types/role.types.ts`, add `tenantId` to the `Role` interface after `createdAt`:

```typescript
  tenantId?: string | null;
```

- [ ] **Step 4: Add getAssignableRoles to roles.api.ts**

In `boilerplateFE/src/features/roles/api/roles.api.ts`, add to the `rolesApi` object:

```typescript
  getAssignableRoles: async (tenantId?: string): Promise<Role[]> => {
    const params = tenantId ? { tenantId } : {};
    const response = await apiClient.get<ApiResponse<Role[]>>(API_ENDPOINTS.ROLES.ASSIGNABLE, { params });
    return response.data.data;
  },
```

- [ ] **Step 5: Add useAssignableRoles hook to roles.queries.ts**

In `boilerplateFE/src/features/roles/api/roles.queries.ts`, add the import for `queryKeys` if not already there, and add the hook:

```typescript
export function useAssignableRoles(tenantId?: string, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: queryKeys.assignableRoles.list(tenantId),
    queryFn: () => rolesApi.getAssignableRoles(tenantId),
    enabled: options?.enabled,
  });
}
```

- [ ] **Step 6: Update auth.api.ts inviteUser payload type**

In `boilerplateFE/src/features/auth/api/auth.api.ts`, change the `inviteUser` method:

```typescript
  inviteUser: (data: { email: string; roleId?: string; tenantId?: string }) =>
    apiClient.post<ApiResponse<string>>(API_ENDPOINTS.AUTH.INVITE_USER, data).then((r) => r.data.data),
```

- [ ] **Step 7: Update useInviteUser mutation type**

In `boilerplateFE/src/features/auth/api/auth.queries.ts`, update the `useInviteUser` function:

```typescript
export function useInviteUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { email: string; roleId?: string; tenantId?: string }) => authApi.inviteUser(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.invitations.all });
      toast.success(i18n.t('invitations.inviteSent'));
    },
  });
}
```

- [ ] **Step 8: Overhaul InviteUserModal**

Replace the contents of `boilerplateFE/src/features/users/components/InviteUserModal.tsx`:

```tsx
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useAssignableRoles } from '@/features/roles/api';
import { useInviteUser } from '@/features/auth/api';
import { useTenants } from '@/features/tenants/api';
import { useAuthStore } from '@/stores';

const inviteUserSchema = z.object({
  email: z.string().min(1, 'Email is required').email('Invalid email address'),
  roleId: z.string().optional(),
  tenantId: z.string().optional(),
});

type InviteUserFormData = z.infer<typeof inviteUserSchema>;

interface InviteUserModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function InviteUserModal({ open, onOpenChange }: InviteUserModalProps) {
  const { t } = useTranslation();
  const user = useAuthStore((state) => state.user);
  const isPlatformAdmin = !user?.tenantId;

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors },
  } = useForm<InviteUserFormData>({
    resolver: zodResolver(inviteUserSchema),
    defaultValues: { email: '', roleId: '', tenantId: '' },
  });

  const selectedTenantId = watch('tenantId');

  // Platform admin: load tenants for the dropdown
  const { data: tenantsData } = useTenants({
    params: { pageNumber: 1, pageSize: 100 },
    enabled: open && isPlatformAdmin,
  });
  const tenants = tenantsData?.data ?? [];

  // Load assignable roles filtered by selected tenant
  const { data: assignableRoles } = useAssignableRoles(
    isPlatformAdmin ? (selectedTenantId || undefined) : undefined,
    { enabled: open }
  );
  const roles = assignableRoles ?? [];

  const { mutate: inviteUser, isPending } = useInviteUser();

  // Reset role when tenant changes
  useEffect(() => {
    setValue('roleId', '');
  }, [selectedTenantId, setValue]);

  const onSubmit = (data: InviteUserFormData) => {
    const payload: { email: string; roleId?: string; tenantId?: string } = {
      email: data.email,
    };
    if (data.roleId) payload.roleId = data.roleId;
    if (isPlatformAdmin && data.tenantId) payload.tenantId = data.tenantId;

    inviteUser(payload, {
      onSuccess: () => {
        reset();
        onOpenChange(false);
      },
    });
  };

  const handleOpenChange = (isOpen: boolean) => {
    if (!isOpen) reset();
    onOpenChange(isOpen);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>{t('invitations.inviteUser')}</DialogTitle>
          <DialogDescription>{t('invitations.inviteUserDesc')}</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="invite-email">{t('common.email')}</Label>
            <Input
              id="invite-email"
              type="email"
              placeholder={t('auth.enterEmail')}
              {...register('email')}
            />
            {errors.email && (
              <p className="text-sm text-destructive">{errors.email.message}</p>
            )}
          </div>

          {/* Tenant selector — platform admin only */}
          {isPlatformAdmin && (
            <div className="space-y-2">
              <Label>{t('invitations.tenant')}</Label>
              <Select onValueChange={(value) => setValue('tenantId', value)}>
                <SelectTrigger>
                  <SelectValue placeholder={t('invitations.selectTenant')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__platform__">{t('invitations.platformLevel')}</SelectItem>
                  {tenants.map((tenant) => (
                    <SelectItem key={tenant.id} value={tenant.id}>
                      {tenant.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">{t('invitations.tenantHint')}</p>
            </div>
          )}

          <div className="space-y-2">
            <Label>{t('invitations.role')}</Label>
            <Select onValueChange={(value) => setValue('roleId', value)}>
              <SelectTrigger>
                <SelectValue placeholder={t('invitations.selectRole')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__default__">{t('invitations.useDefaultRole')}</SelectItem>
                {roles.map((role) => (
                  <SelectItem key={role.id} value={role.id}>
                    {role.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">{t('invitations.roleHint')}</p>
          </div>

          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? t('common.loading') : t('invitations.sendInvite')}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 9: Build verification**

```bash
cd boilerplateFE && npm run build
```

**Commit:** `feat(frontend): overhaul InviteUserModal with tenant dropdown and assignable roles`

---

## Task 10: Frontend — Roles Page System/Custom Badges, Flag-Gated Create

**Files:**
- Modify: `boilerplateFE/src/features/roles/pages/RolesListPage.tsx`

- [ ] **Step 1: Update RolesListPage with system/custom badges and flag-gated Create**

Replace the contents of `boilerplateFE/src/features/roles/pages/RolesListPage.tsx`:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Plus, Shield } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader, EmptyState, Pagination, getPersistedPageSize } from '@/components/common';
import { useRoles } from '../api';
import { usePermissions } from '@/hooks';
import { useAuthStore } from '@/stores';
import { PERMISSIONS } from '@/constants';
import { ROUTES } from '@/config';
import { useFeatureFlag } from '@/hooks/useFeatureFlag';

export default function RolesListPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const user = useAuthStore((state) => state.user);
  const isTenantUser = !!user?.tenantId;
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(getPersistedPageSize);
  const { data, isLoading, isError } = useRoles({ params: { pageNumber, pageSize } });
  const roles = data?.data ?? [];
  const pagination = data?.pagination;

  // Feature flag: tenant custom roles enabled
  const { isEnabled: customRolesEnabled, isLoading: flagLoading } = useFeatureFlag('roles.tenant_custom_enabled');

  // Show Create button only if:
  // - User has Roles.Create permission
  // - AND either: user is platform admin OR custom roles flag is enabled for tenant users
  const canCreate = hasPermission(PERMISSIONS.Roles.Create) && (!isTenantUser || customRolesEnabled);

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('roles.title')} />
        <EmptyState icon={Shield} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
      </div>
    );
  }

  if (isLoading && !data) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('roles.title')}
        subtitle={t('roles.allRoles')}
        actions={
          canCreate ? (
            <Link to={ROUTES.ROLES.CREATE}>
              <Button>
                <Plus className="h-4 w-4" />
                {t('roles.createRole')}
              </Button>
            </Link>
          ) : undefined
        }
      />

      {roles.length === 0 ? (
        <EmptyState icon={Shield} title={t('common.noResults')} />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {roles.map((role) => (
            <Link key={role.id} to={ROUTES.ROLES.getDetail(role.id)}>
              <Card className="hover:shadow-card-hover transition-all duration-200 cursor-pointer h-full">
                <CardContent className="py-4">
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-lg [background:var(--active-bg)]">
                      <Shield className="h-5 w-5 [color:var(--active-text)]" />
                    </div>
                    <div className="flex items-center gap-1.5">
                      {role.isSystemRole ? (
                        <Badge variant="outline">{t('roles.system')}</Badge>
                      ) : (
                        <Badge variant="secondary">{t('roles.custom')}</Badge>
                      )}
                      <Badge variant={role.isActive ? 'default' : 'secondary'}>
                        {role.isActive ? t('common.active') : t('common.inactive')}
                      </Badge>
                    </div>
                  </div>
                  <h3 className="font-semibold text-foreground">{role.name}</h3>
                  {role.description && (
                    <p className="mt-1 text-sm text-muted-foreground line-clamp-2">{role.description}</p>
                  )}
                  <div className="mt-3 flex items-center gap-4 text-xs text-muted-foreground">
                    <span>{role.userCount} {t('roles.roleUsers').toLowerCase()}</span>
                    <span>{role.permissions?.length || 0} {t('roles.rolePermissions').toLowerCase()}</span>
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}

      {pagination && (
        <Pagination
          pagination={pagination}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => { setPageSize(size); setPageNumber(1); }}
        />
      )}
    </div>
  );
}
```

> **Note:** This requires `useFeatureFlag` hook at `src/hooks/useFeatureFlag.ts`. If it does not exist, check `src/hooks/` for the existing hook. If it uses a different name or pattern (e.g., importing from the feature-flags feature), adapt accordingly. The hook should return `{ isEnabled: boolean, isLoading: boolean }` for a given flag key.

- [ ] **Step 2: Build verification**

```bash
cd boilerplateFE && npm run build
```

**Commit:** `feat(frontend): add system/custom role badges, feature-flag-gated Create button on RolesListPage`

---

## Task 11: Frontend — Tenant Detail Default Registration Role Dropdown

**Files:**
- Modify: `boilerplateFE/src/features/tenants/pages/TenantDetailPage.tsx`
- Modify: `boilerplateFE/src/features/tenants/api/tenants.api.ts`
- Modify: `boilerplateFE/src/features/tenants/api/tenants.queries.ts`
- Modify: `boilerplateFE/src/types/tenant.types.ts`

- [ ] **Step 1: Add defaultRegistrationRoleId and defaultRoleName to Tenant type**

In `boilerplateFE/src/types/tenant.types.ts`, add to the `Tenant` interface after `faviconUrl`:

```typescript
  defaultRegistrationRoleId: string | null;
  defaultRoleName: string | null;
```

- [ ] **Step 2: Add setDefaultRole to tenants.api.ts**

In `boilerplateFE/src/features/tenants/api/tenants.api.ts`, add to the `tenantsApi` object:

```typescript
  setDefaultRole: (id: string, roleId: string | null) =>
    apiClient.put(API_ENDPOINTS.TENANTS.DEFAULT_ROLE(id), { roleId }).then((r) => r.data),
```

- [ ] **Step 3: Add useSetTenantDefaultRole hook to tenants.queries.ts**

In `boilerplateFE/src/features/tenants/api/tenants.queries.ts`, add the hook (with necessary imports):

```typescript
export function useSetTenantDefaultRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, roleId }: { id: string; roleId: string | null }) =>
      tenantsApi.setDefaultRole(id, roleId),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.tenants.detail(variables.id) });
      toast.success(i18n.t('tenants.defaultRoleUpdated'));
    },
  });
}
```

- [ ] **Step 4: Add default role dropdown to TenantDetailPage overview tab**

In `boilerplateFE/src/features/tenants/pages/TenantDetailPage.tsx`:

1. Add imports at the top:
```typescript
import { useAssignableRoles } from '@/features/roles/api';
import { useSetTenantDefaultRole } from '../api';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
```

2. Inside the component, add after the status mutations block:
```typescript
  // Default registration role
  const { mutate: setDefaultRole, isPending: isSavingDefaultRole } = useSetTenantDefaultRole();
  const { data: assignableRoles } = useAssignableRoles(id, { enabled: !!id && activeTab === 'overview' });
  const availableRoles = assignableRoles ?? [];
```

3. In the overview tab, add a default role section before the status action buttons section (before `<div className="flex items-center gap-2 border-t pt-4 mt-6">`):

```tsx
                {/* Default Registration Role */}
                {hasPermission(PERMISSIONS.Tenants.Update) && (
                  <div className="border-t pt-4 mt-6">
                    <h4 className="text-sm font-medium text-foreground mb-2">{t('tenants.defaultRegistrationRole')}</h4>
                    <p className="text-xs text-muted-foreground mb-3">{t('tenants.defaultRegistrationRoleDesc')}</p>
                    <div className="flex items-center gap-3">
                      <Select
                        value={tenant.defaultRegistrationRoleId ?? '__none__'}
                        onValueChange={(value) => {
                          const roleId = value === '__none__' ? null : value;
                          setDefaultRole({ id: id!, roleId });
                        }}
                      >
                        <SelectTrigger className="max-w-xs">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="__none__">{t('tenants.useGlobalDefault')}</SelectItem>
                          {availableRoles.map((role) => (
                            <SelectItem key={role.id} value={role.id}>
                              {role.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      {isSavingDefaultRole && <Spinner size="sm" />}
                    </div>
                  </div>
                )}
```

- [ ] **Step 5: Build verification**

```bash
cd boilerplateFE && npm run build
```

**Commit:** `feat(frontend): add default registration role dropdown to TenantDetailPage`

---

## Task 12: i18n (3 Locales) + Permissions Constants

**Files:**
- Modify: `boilerplateFE/src/i18n/locales/en/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ar/translation.json`
- Modify: `boilerplateFE/src/i18n/locales/ku/translation.json`

- [ ] **Step 1: Add English i18n keys**

In `boilerplateFE/src/i18n/locales/en/translation.json`, add/update the following keys in the appropriate sections:

In the `invitations` section, add:
```json
    "tenant": "Tenant",
    "selectTenant": "Select a tenant",
    "platformLevel": "Platform Level (no tenant)",
    "tenantHint": "Leave empty for platform-level invitation",
    "useDefaultRole": "Use Default Role",
    "roleHint": "Leave as default to use the tenant or global default role"
```

In the `roles` section, add:
```json
    "system": "System",
    "custom": "Custom",
    "customRolesDisabled": "Custom roles are not enabled for your organization"
```

In the `tenants` section, add:
```json
    "defaultRegistrationRole": "Default Registration Role",
    "defaultRegistrationRoleDesc": "New users invited to this tenant will be assigned this role by default",
    "useGlobalDefault": "Use Global Default",
    "defaultRoleUpdated": "Default registration role updated"
```

- [ ] **Step 2: Add Arabic i18n keys**

In `boilerplateFE/src/i18n/locales/ar/translation.json`, add the matching keys:

In the `invitations` section:
```json
    "tenant": "المستأجر",
    "selectTenant": "اختر مستأجر",
    "platformLevel": "مستوى المنصة (بدون مستأجر)",
    "tenantHint": "اتركه فارغاً لدعوة على مستوى المنصة",
    "useDefaultRole": "استخدم الدور الافتراضي",
    "roleHint": "اتركه كافتراضي لاستخدام الدور الافتراضي للمستأجر أو العام"
```

In the `roles` section:
```json
    "system": "نظامي",
    "custom": "مخصص",
    "customRolesDisabled": "الأدوار المخصصة غير مفعلة لمؤسستك"
```

In the `tenants` section:
```json
    "defaultRegistrationRole": "دور التسجيل الافتراضي",
    "defaultRegistrationRoleDesc": "سيتم تعيين هذا الدور افتراضياً للمستخدمين الجدد المدعوين لهذا المستأجر",
    "useGlobalDefault": "استخدم الافتراضي العام",
    "defaultRoleUpdated": "تم تحديث دور التسجيل الافتراضي"
```

- [ ] **Step 3: Add Kurdish i18n keys**

In `boilerplateFE/src/i18n/locales/ku/translation.json`, add the matching keys:

In the `invitations` section:
```json
    "tenant": "خاوەنداری",
    "selectTenant": "خاوەندارییەک هەڵبژێرە",
    "platformLevel": "ئاستی سەکۆ (بێ خاوەنداری)",
    "tenantHint": "بەتاڵی بهێڵەوە بۆ بانگهێشتی ئاستی سەکۆ",
    "useDefaultRole": "ڕۆڵی بنەڕەتی بەکاربهێنە",
    "roleHint": "وەک بنەڕەتی بهێڵەوە بۆ بەکارهێنانی ڕۆڵی بنەڕەتی خاوەنداری یان گشتی"
```

In the `roles` section:
```json
    "system": "سیستەم",
    "custom": "تایبەت",
    "customRolesDisabled": "ڕۆڵە تایبەتەکان بۆ دامەزراوەکەت چالاک نەکراون"
```

In the `tenants` section:
```json
    "defaultRegistrationRole": "ڕۆڵی تۆمارکردنی بنەڕەتی",
    "defaultRegistrationRoleDesc": "بەکارهێنەرە نوێکانی بانگهێشتکراو بۆ ئەم خاوەندارییە ئەم ڕۆڵەیان پێ دەدرێت بە بنەڕەت",
    "useGlobalDefault": "بنەڕەتی گشتی بەکاربهێنە",
    "defaultRoleUpdated": "ڕۆڵی تۆمارکردنی بنەڕەتی نوێ کرایەوە"
```

- [ ] **Step 4: Build verification**

```bash
cd boilerplateFE && npm run build
```

**Commit:** `feat(i18n): add invitation system overhaul translations for en, ar, ku`
