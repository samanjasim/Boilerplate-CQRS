using MediatR;
using Starter.Application.Features.ImportExport.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ImportExport.Queries.GetImportJobById;

public sealed record GetImportJobByIdQuery(Guid Id) : IRequest<Result<ImportJobDto>>;
