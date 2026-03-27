using MediatR;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.CreateApiKey;

public sealed record CreateApiKeyCommand(
    string Name,
    List<string> Scopes,
    DateTime? ExpiresAt,
    bool IsPlatformKey = false) : IRequest<Result<CreateApiKeyResponse>>;
