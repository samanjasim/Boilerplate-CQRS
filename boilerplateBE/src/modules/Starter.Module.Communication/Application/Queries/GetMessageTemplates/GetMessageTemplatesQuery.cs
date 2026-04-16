using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetMessageTemplates;

public sealed record GetMessageTemplatesQuery(string? Category = null) : IRequest<Result<List<MessageTemplateDto>>>;
