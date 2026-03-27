using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.RevokeApiKey;

public sealed record RevokeApiKeyCommand(Guid Id) : IRequest<Result>;
