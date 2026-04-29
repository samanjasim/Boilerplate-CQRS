using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets;

internal static class AiPublicWidgetMappings
{
    public static AiPublicWidgetDto ToDto(AiPublicWidget widget) =>
        new(
            widget.Id,
            widget.Name,
            widget.Status,
            widget.AllowedOrigins.ToList(),
            widget.DefaultAssistantId,
            widget.DefaultPersonaSlug,
            widget.MonthlyTokenCap,
            widget.DailyTokenCap,
            widget.RequestsPerMinute,
            widget.MetadataJson,
            widget.CreatedAt);

    public static AiWidgetCredentialDto ToDto(AiWidgetCredential credential) =>
        new(
            credential.Id,
            credential.WidgetId,
            credential.KeyPrefix,
            $"{credential.KeyPrefix}****",
            credential.Status,
            credential.ExpiresAt,
            credential.LastUsedAt,
            credential.CreatedAt);
}
