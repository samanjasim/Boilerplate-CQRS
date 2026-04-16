using MediatR;
using Starter.Module.Communication.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.UpdateTemplateOverride;

public sealed record UpdateTemplateOverrideCommand(
    Guid MessageTemplateId,
    string? SubjectTemplate,
    string BodyTemplate) : IRequest<Result<MessageTemplateOverrideDto>>;
