using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.ReprocessDocument;

public sealed record ReprocessDocumentCommand(Guid Id) : IRequest<Result>;
