using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Webhooks.Commands.RegenerateWebhookSecret;

public sealed record RegenerateWebhookSecretCommand(Guid Id) : IRequest<Result<string>>;
