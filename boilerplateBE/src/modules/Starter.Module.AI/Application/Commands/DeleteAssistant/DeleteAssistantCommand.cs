using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeleteAssistant;

public sealed record DeleteAssistantCommand(Guid Id) : IRequest<Result>;
