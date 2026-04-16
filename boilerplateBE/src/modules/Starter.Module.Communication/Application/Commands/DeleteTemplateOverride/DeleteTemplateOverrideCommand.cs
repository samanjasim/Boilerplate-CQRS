using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Commands.DeleteTemplateOverride;

public sealed record DeleteTemplateOverrideCommand(Guid MessageTemplateId) : IRequest<Result>;
