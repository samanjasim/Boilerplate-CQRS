using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Interfaces;
using Starter.Application.Common.Models;
using Starter.Domain.Identity.Entities;
using Starter.Domain.ImportExport.Enums;

namespace Starter.Application.Features.ImportExport.Definitions;

public sealed class RoleImportRowProcessor(IApplicationDbContext context) : IImportRowProcessor
{
    public async Task<ImportRowResult> ProcessRowAsync(
        Dictionary<string, string> row,
        ConflictMode conflictMode,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        // Validate required fields
        if (!row.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
            return new ImportRowResult(ImportRowStatus.Failed, ErrorMessage: "Name is required.");

        name = name.Trim();

        if (name.Length > 200)
            return new ImportRowResult(ImportRowStatus.Failed, ErrorMessage: "Name cannot exceed 200 characters.");

        row.TryGetValue("Description", out var description);
        if (!string.IsNullOrWhiteSpace(description) && description.Length > 500)
            return new ImportRowResult(ImportRowStatus.Failed, ErrorMessage: "Description cannot exceed 500 characters.");

        // Check for existing role with same name within the tenant
        var existingRole = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name.ToLower() == name.ToLower(), ct);

        if (existingRole is not null)
        {
            if (conflictMode == ConflictMode.Skip)
                return new ImportRowResult(ImportRowStatus.Skipped, ErrorMessage: "Role name already exists.");

            if (conflictMode == ConflictMode.Upsert)
            {
                existingRole.Update(name, string.IsNullOrWhiteSpace(description) ? null : description.Trim());
                return new ImportRowResult(ImportRowStatus.Updated, EntityId: existingRole.Id.ToString());
            }
        }

        // Create new non-system role scoped to the tenant
        var newRole = Role.Create(
            name,
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            isSystemRole: false,
            tenantId: tenantId);

        context.Roles.Add(newRole);

        return new ImportRowResult(ImportRowStatus.Created, EntityId: newRole.Id.ToString());
    }
}
