using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Files.Queries.GetFileById;

public sealed record GetFileByIdQuery(Guid Id) : IRequest<Result<FileDto>>;
