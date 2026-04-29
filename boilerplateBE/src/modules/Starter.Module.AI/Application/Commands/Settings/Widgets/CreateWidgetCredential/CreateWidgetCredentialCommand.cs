using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets.CreateWidgetCredential;

public sealed record CreateWidgetCredentialCommand(Guid WidgetId, DateTimeOffset? ExpiresAt)
    : IRequest<Result<CreateAiWidgetCredentialResponse>>;
