using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetChannelConfigById;

public sealed record GetChannelConfigByIdQuery(Guid Id) : IRequest<Result<ChannelConfigDetailDto>>;
