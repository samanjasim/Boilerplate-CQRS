using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets.UpdatePublicWidget;

public sealed record UpdatePublicWidgetCommand(
    Guid Id,
    string Name,
    IReadOnlyList<string> AllowedOrigins,
    Guid? DefaultAssistantId,
    string DefaultPersonaSlug,
    int? MonthlyTokenCap,
    int? DailyTokenCap,
    int? RequestsPerMinute,
    AiPublicWidgetStatus Status,
    string? MetadataJson) : IRequest<Result<AiPublicWidgetDto>>;
