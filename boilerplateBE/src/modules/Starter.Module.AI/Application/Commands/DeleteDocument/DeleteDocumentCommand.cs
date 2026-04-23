using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.DeleteDocument;

public sealed record DeleteDocumentCommand(Guid Id) : IRequest<Result>;
