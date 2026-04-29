using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Settings.Widgets.GetPublicWidgets;

public sealed record GetPublicWidgetsQuery(Guid? TenantId)
    : IRequest<Result<IReadOnlyList<AiPublicWidgetDto>>>;
