using MediatR;
using Starter.Application.Features.ApiKeys.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ApiKeys.Queries.GetApiKeyById;

public sealed record GetApiKeyByIdQuery(Guid Id) : IRequest<Result<ApiKeyDto>>;
