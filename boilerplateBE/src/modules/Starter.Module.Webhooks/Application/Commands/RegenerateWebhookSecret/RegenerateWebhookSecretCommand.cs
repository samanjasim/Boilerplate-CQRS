using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Webhooks.Application.Commands.RegenerateWebhookSecret;

public sealed record RegenerateWebhookSecretCommand(Guid Id) : IRequest<Result<string>>;
