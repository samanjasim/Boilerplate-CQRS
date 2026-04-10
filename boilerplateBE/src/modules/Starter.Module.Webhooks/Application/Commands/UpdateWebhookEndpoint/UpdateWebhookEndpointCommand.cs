using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Commands.UpdateWebhookEndpoint;

public sealed record UpdateWebhookEndpointCommand(
    Guid Id,
    string Url,
    string? Description,
    string[] Events,
    bool IsActive) : IRequest<Result<Unit>>;
