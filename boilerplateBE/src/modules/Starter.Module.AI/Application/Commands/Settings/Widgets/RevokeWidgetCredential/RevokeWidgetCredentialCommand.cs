using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.Widgets.RevokeWidgetCredential;

public sealed record RevokeWidgetCredentialCommand(Guid WidgetId, Guid CredentialId) : IRequest<Result>;
