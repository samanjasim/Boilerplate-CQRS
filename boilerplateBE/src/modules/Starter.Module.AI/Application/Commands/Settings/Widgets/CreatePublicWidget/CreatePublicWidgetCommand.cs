using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets.CreatePublicWidget;

public sealed record CreatePublicWidgetCommand(
    Guid? TenantId,
    string Name,
    IReadOnlyList<string> AllowedOrigins,
    Guid? DefaultAssistantId,
    string DefaultPersonaSlug,
    int? MonthlyTokenCap,
    int? DailyTokenCap,
    int? RequestsPerMinute,
    string? MetadataJson) : IRequest<Result<AiPublicWidgetDto>>;
