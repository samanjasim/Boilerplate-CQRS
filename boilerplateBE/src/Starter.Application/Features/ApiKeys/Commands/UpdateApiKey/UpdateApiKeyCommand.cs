using MediatR;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Commands.UpdateApiKey;

public sealed record UpdateApiKeyCommand(
    Guid Id,
    string? Name,
    List<string>? Scopes) : IRequest<Result<ApiKeyDto>>;
