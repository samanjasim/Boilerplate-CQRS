using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Domain.Identity.Entities;
using Starter.Domain.Identity.ValueObjects;
using Starter.Domain.ImportExport.Enums;

namespace Starter.Application.Features.ImportExport.Definitions;

public sealed class UserImportRowProcessor(
    IApplicationDbContext context,
    IPasswordService passwordService) : IImportRowProcessor
{
    public async Task<ImportRowResult> ProcessRowAsync(
        Dictionary<string, string> row,
        ConflictMode conflictMode,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        // Validate required fields
        if (!row.TryGetValue("Email", out var email) || string.IsNullOrWhiteSpace(email))
            return new ImportRowResult(ImportRowStatus.Failed, ErrorMessage: "Email is required.");

        if (!row.TryGetValue("FirstName", out var firstName) || string.IsNullOrWhiteSpace(firstName))
            return new ImportRowResult(ImportRowStatus.Failed, ErrorMessage: "First Name is required.");

        if (!row.TryGetValue("LastName", out var lastName) || string.IsNullOrWhiteSpace(lastName))
            return new ImportRowResult(ImportRowStatus.Failed, ErrorMessage: "Last Name is required.");

        if (!row.TryGetValue("Username", out var username) || string.IsNullOrWhiteSpace(username))
            return new ImportRowResult(ImportRowStatus.Failed, ErrorMessage: "Username is required.");

        // Validate email format
        if (!Email.TryCreate(email, out var emailValue) || emailValue is null)
            return new ImportRowResult(ImportRowStatus.Failed, ErrorMessage: "Email format is invalid.");

        // Validate name lengths
        if (!FullName.TryCreate(firstName, lastName, out var fullName) || fullName is null)
            return new ImportRowResult(ImportRowStatus.Failed, ErrorMessage: "First Name or Last Name is invalid.");

        var normalizedEmail = Email.Normalize(email);

        // Check if user already exists within the tenant
        var existingUser = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email.Value == normalizedEmail, ct);

        if (existingUser is not null)
        {
            if (conflictMode == ConflictMode.Skip)
                return new ImportRowResult(ImportRowStatus.Skipped, ErrorMessage: "Email already exists.");

            if (conflictMode == ConflictMode.Upsert)
            {
                existingUser.UpdateProfile(fullName, existingUser.PhoneNumber);
                return new ImportRowResult(ImportRowStatus.Updated, EntityId: existingUser.Id.ToString());
            }
        }

        // Create new user with a random password (forces reset on first login)
        var randomPassword = $"Imp@{Guid.NewGuid():N}"[..16];
        var passwordHash = await passwordService.HashPasswordAsync(randomPassword);
        var newUser = User.Create(username.Trim(), emailValue, fullName, passwordHash, tenantId);

        context.Users.Add(newUser);

        return new ImportRowResult(ImportRowStatus.Created, EntityId: newUser.Id.ToString());
    }
}
