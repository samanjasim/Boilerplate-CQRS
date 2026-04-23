using MediatR;
using Starter.Domain.Common.Access.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.SetAssistantAccessMode;

public sealed record SetAssistantAccessModeCommand(Guid Id, AssistantAccessMode AccessMode) : IRequest<Result>;
