using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetAvailableProviders;

public sealed record GetAvailableProvidersQuery : IRequest<Result<List<AvailableProviderDto>>>;
