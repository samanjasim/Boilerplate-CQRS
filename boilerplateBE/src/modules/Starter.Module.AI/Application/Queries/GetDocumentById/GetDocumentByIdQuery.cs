using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetDocumentById;

public sealed record GetDocumentByIdQuery(Guid Id, int ChunkPreviewLimit = 20)
    : IRequest<Result<AiDocumentDetailDto>>;
