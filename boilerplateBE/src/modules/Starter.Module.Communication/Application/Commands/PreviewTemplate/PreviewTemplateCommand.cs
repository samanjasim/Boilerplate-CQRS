using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.PreviewTemplate;

public sealed record PreviewTemplateCommand(
    Guid MessageTemplateId,
    Dictionary<string, object>? Variables) : IRequest<Result<TemplatePreviewDto>>;
