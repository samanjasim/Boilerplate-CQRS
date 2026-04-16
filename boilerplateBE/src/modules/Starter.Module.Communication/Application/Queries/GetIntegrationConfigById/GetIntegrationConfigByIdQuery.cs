using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetIntegrationConfigById;

public sealed record GetIntegrationConfigByIdQuery(Guid Id) : IRequest<Result<IntegrationConfigDto>>;
