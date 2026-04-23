using MediatR;
using Starter.Abstractions.Paging;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetDocuments;

public sealed record GetDocumentsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null,
    string? SearchTerm = null) : IRequest<Result<PaginatedList<AiDocumentDto>>>;
