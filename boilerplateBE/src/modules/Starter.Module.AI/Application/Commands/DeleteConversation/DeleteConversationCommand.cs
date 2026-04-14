using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeleteConversation;

public sealed record DeleteConversationCommand(Guid Id) : IRequest<Result>;
