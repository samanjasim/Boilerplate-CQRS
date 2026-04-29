using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.DTOs;

internal static class AiProviderCredentialDtos
{
    public static AiProviderCredentialDto ToDto(AiProviderCredential credential, IAiSecretProtector secrets) =>
        new(
            credential.Id,
            credential.Provider,
            credential.DisplayName,
            secrets.Mask(credential.KeyPrefix),
            credential.Status,
            credential.LastValidatedAt,
            credential.LastUsedAt,
            credential.CreatedAt);
}
