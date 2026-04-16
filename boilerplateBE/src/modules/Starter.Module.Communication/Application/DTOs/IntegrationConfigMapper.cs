using System.Text.Json;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Application.DTOs;

public static class IntegrationConfigMapper
{
    public static IntegrationConfigDto ToDto(this IntegrationConfig entity,
        Dictionary<string, string>? maskedCredentials = null)
    {
        Dictionary<string, string>? channelMappings = null;
        if (!string.IsNullOrWhiteSpace(entity.ChannelMappingsJson))
        {
            try
            {
                channelMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    entity.ChannelMappingsJson);
            }
            catch
            {
                // Ignore deserialization errors — return null
            }
        }

        return new IntegrationConfigDto(
            Id: entity.Id,
            IntegrationType: entity.IntegrationType,
            DisplayName: entity.DisplayName,
            MaskedCredentials: maskedCredentials,
            ChannelMappings: channelMappings,
            Status: entity.Status,
            LastTestedAt: entity.LastTestedAt,
            LastTestResult: entity.LastTestResult,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }
}
