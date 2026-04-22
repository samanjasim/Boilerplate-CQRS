using Starter.Application.Common.Models;
using Starter.Domain.Common.Enums;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Files.Queries.GetFiles;

public sealed record GetFilesQuery : PaginationQuery, IRequest<Result<PaginatedList<FileDto>>>
{
    public FileCategory? Category { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? Origin { get; init; }
    public string? View { get; init; }
}
