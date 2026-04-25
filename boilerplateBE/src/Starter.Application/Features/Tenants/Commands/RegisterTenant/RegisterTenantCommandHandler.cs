using Starter.Application.Common.Constants;
using Starter.Application.Common.Events;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.Errors;
using Starter.Domain.Identity.ValueObjects;
using Starter.Domain.Tenants.Entities;
using Starter.Domain.Tenants.Errors;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using RoleConstants = Starter.Shared.Constants.Roles;

namespace Starter.Application.Features.Tenants.Commands.RegisterTenant;

internal sealed class RegisterTenantCommandHandler(
    IApplicationDbContext context,
    IPasswordService passwordService,
    IOtpService otpService,
    IEmailTemplateService emailTemplateService,
    IIntegrationEventCollector eventCollector) : IRequestHandler<RegisterTenantCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = Email.Normalize(request.Email);

        // Check email uniqueness across ALL users (ignore query filters)
        var emailExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.Value == normalizedEmail, cancellationToken);

        if (emailExists)
            return Result.Failure<Guid>(UserErrors.EmailAlreadyExists(request.Email));

        // Generate slug from company name
        var slug = GenerateSlug(request.CompanyName);

        // Check slug uniqueness
        var slugExists = await context.Tenants
            .AnyAsync(t => t.Slug == slug, cancellationToken);

        if (slugExists)
            return Result.Failure<Guid>(TenantErrors.SlugAlreadyExists(slug));

        // Create tenant
        var tenant = Tenant.Create(request.CompanyName, slug);
        context.Tenants.Add(tenant);

        // Hash password and create user
        var email = Email.Create(request.Email);
        var fullName = FullName.Create(request.FirstName, request.LastName);
        var passwordHash = await passwordService.HashPasswordAsync(request.Password);

        var user = User.Create(
            normalizedEmail,
            email,
            fullName,
            passwordHash,
            tenantId: tenant.Id);

        // Resolve tenant owner role: setting → fallback to system "Admin"
        var ownerRole = await ResolveTenantOwnerRoleAsync(cancellationToken);
        if (ownerRole is not null)
            user.AddRole(ownerRole);

        context.Users.Add(user);

        // Generate OTP + render email BEFORE the commit. OTP write is to Redis
        // (its own TTL); rendering is pure. The resulting EmailMessage rides on
        // a SendEmailRequestedEvent that lands in the outbox atomically with the
        // tenant + user rows — so if SMTP is briefly down at dispatch time,
        // MT's retry + DLQ handle it without the tenant being stranded without
        // a verification email.
        var otpCode = await otpService.GenerateAsync(OtpPurpose.EmailVerification, user.Email.Value, cancellationToken);
        var emailMessage = emailTemplateService.RenderEmailVerification(user.Email.Value, user.FullName.GetFullName(), otpCode);

        // Schedule integration events for transactional-outbox delivery.
        // IntegrationEventOutboxInterceptor drains the collector during
        // SavingChangesAsync and writes outbox rows on the same DbContext
        // transaction — see docs/architecture/cross-module-communication.md.
        eventCollector.Schedule(
            new TenantRegisteredEvent(
                tenant.Id,
                tenant.Name,
                tenant.Slug ?? string.Empty,
                user.Id,
                DateTime.UtcNow));
        eventCollector.Schedule(new SendEmailRequestedEvent(emailMessage, DateTime.UtcNow));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(tenant.Id);
    }

    /// <summary>
    /// Resolves the role for a new tenant owner:
    /// 1. registration.tenant_owner_role_id setting → 2. Fallback to system "Admin" role
    /// </summary>
    private async Task<Role?> ResolveTenantOwnerRoleAsync(CancellationToken cancellationToken)
    {
        var setting = await context.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s =>
                s.Key == "registration.tenant_owner_role_id" &&
                s.TenantId == null,
                cancellationToken);

        if (setting is not null && Guid.TryParse(setting.Value, out var roleId))
        {
            var role = await context.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

            if (role is not null) return role;
        }

        // Fallback: system "Admin" role
        return await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Name == RoleConstants.Admin && r.IsSystemRole, cancellationToken);
    }

    private static string GenerateSlug(string companyName)
    {
        var slug = companyName.ToLowerInvariant().Trim();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"[\s]+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');
        return slug;
    }
}
