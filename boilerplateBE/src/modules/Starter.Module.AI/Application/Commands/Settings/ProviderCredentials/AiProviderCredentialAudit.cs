using System.Text.Json;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common;
using Starter.Domain.Common.Enums;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.Commands.Settings.ProviderCredentials;

internal static class AiProviderCredentialAudit
{
    public static void Add(
        IApplicationDbContext coreDb,
        ICurrentUserService currentUser,
        AiProviderCredential credential,
        string actionCode,
        AuditAction action)
    {
        coreDb.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = AuditEntityType.AiProviderCredential,
            EntityId = credential.Id,
            Action = action,
            Changes = JsonSerializer.Serialize(new
            {
                actionCode,
                Id = credential.Id,
                Provider = credential.Provider.ToString(),
                KeyPrefix = credential.KeyPrefix
            }),
            PerformedBy = currentUser.UserId,
            PerformedByName = currentUser.Email,
            PerformedAt = DateTime.UtcNow,
            TenantId = credential.TenantId
        });
    }
}
