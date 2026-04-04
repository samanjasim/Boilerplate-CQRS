using MediatR;
using Starter.Application.Features.ImportExport.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.ImportExport.Queries.PreviewImport;

public sealed record PreviewImportQuery(Guid FileId, string EntityType) : IRequest<Result<ImportPreviewDto>>;
