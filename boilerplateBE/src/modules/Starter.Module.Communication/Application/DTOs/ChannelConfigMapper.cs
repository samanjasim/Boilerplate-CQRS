using System.Text.Json;
using Starter.Module.Communication.Domain.Entities;

namespace Starter.Module.Communication.Application.DTOs;

public static class ChannelConfigMapper
{
    public static ChannelConfigDto ToDto(this ChannelConfig entity)
    {
        return new ChannelConfigDto(
            Id: entity.Id,
            Channel: entity.Channel,
            Provider: entity.Provider,
            DisplayName: entity.DisplayName,
            Status: entity.Status,
            IsDefault: entity.IsDefault,
            LastTestedAt: entity.LastTestedAt,
            LastTestResult: entity.LastTestResult,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }

    public static ChannelConfigDetailDto ToDetailDto(this ChannelConfig entity, Dictionary<string, string> maskedCredentials)
    {
        return new ChannelConfigDetailDto(
            Id: entity.Id,
            Channel: entity.Channel,
            Provider: entity.Provider,
            DisplayName: entity.DisplayName,
            MaskedCredentials: maskedCredentials,
            Status: entity.Status,
            IsDefault: entity.IsDefault,
            LastTestedAt: entity.LastTestedAt,
            LastTestResult: entity.LastTestResult,
            CreatedAt: entity.CreatedAt,
            ModifiedAt: entity.ModifiedAt);
    }
}
