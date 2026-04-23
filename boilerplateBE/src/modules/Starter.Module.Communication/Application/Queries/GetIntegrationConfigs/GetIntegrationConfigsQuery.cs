using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetIntegrationConfigs;

public sealed record GetIntegrationConfigsQuery : IRequest<Result<List<IntegrationConfigDto>>>;
