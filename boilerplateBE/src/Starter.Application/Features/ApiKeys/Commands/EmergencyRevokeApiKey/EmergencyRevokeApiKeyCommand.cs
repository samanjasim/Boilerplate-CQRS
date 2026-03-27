using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.EmergencyRevokeApiKey;

public sealed record EmergencyRevokeApiKeyCommand(
    Guid Id,
    string? Reason = null) : IRequest<Result>;
