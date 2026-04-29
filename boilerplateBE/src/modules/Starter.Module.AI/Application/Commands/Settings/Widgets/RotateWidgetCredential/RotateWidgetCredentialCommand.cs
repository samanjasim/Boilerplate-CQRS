using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets.RotateWidgetCredential;

public sealed record RotateWidgetCredentialCommand(Guid WidgetId, Guid CredentialId, DateTimeOffset? ExpiresAt)
    : IRequest<Result<CreateAiWidgetCredentialResponse>>;
