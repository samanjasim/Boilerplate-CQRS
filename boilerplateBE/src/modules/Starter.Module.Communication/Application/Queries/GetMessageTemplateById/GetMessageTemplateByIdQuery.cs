using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetMessageTemplateById;

public sealed record GetMessageTemplateByIdQuery(Guid Id) : IRequest<Result<MessageTemplateDetailDto>>;
