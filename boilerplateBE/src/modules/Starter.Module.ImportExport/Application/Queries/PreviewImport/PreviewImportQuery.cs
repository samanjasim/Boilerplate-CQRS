using MediatR;
using Starter.Module.ImportExport.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Queries.PreviewImport;

public sealed record PreviewImportQuery(Guid FileId, string EntityType) : IRequest<Result<ImportPreviewDto>>;
