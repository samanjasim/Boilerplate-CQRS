using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Commands.CreateWebhookEndpoint;

public sealed record CreateWebhookEndpointCommand(
    string Url,
    string? Description,
    string[] Events,
    bool IsActive) : IRequest<Result<CreateWebhookEndpointResponse>>;

public sealed record CreateWebhookEndpointResponse(Guid Id, string Secret);
