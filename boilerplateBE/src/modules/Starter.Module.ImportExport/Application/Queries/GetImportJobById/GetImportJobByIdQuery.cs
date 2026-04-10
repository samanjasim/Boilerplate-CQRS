using MediatR;
using Starter.Module.ImportExport.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Queries.GetImportJobById;

public sealed record GetImportJobByIdQuery(Guid Id) : IRequest<Result<ImportJobDto>>;
