using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetChannelConfigs;

public sealed record GetChannelConfigsQuery : IRequest<Result<List<ChannelConfigDto>>>;
