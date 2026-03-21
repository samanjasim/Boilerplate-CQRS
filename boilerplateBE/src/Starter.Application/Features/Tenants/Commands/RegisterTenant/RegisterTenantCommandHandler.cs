using Starter.Application.Common.Constants;
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
    IEmailService emailService,
    IEmailTemplateService emailTemplateService) : IRequestHandler<RegisterTenantCommand, Result<Guid>>
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

        // Find the "Admin" system role and assign it
        var adminRole = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Name == RoleConstants.Admin && r.IsSystemRole, cancellationToken);

        if (adminRole is not null)
            user.AddRole(adminRole);

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        // Send email verification OTP
        var otpCode = await otpService.GenerateAsync(OtpPurpose.EmailVerification, user.Email.Value, cancellationToken);
        var emailMessage = emailTemplateService.RenderEmailVerification(user.Email.Value, user.FullName.GetFullName(), otpCode);
        await emailService.SendAsync(emailMessage, cancellationToken);

        return Result.Success(tenant.Id);
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
