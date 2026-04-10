using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Queries.GetImportTemplate;

public sealed record GetImportTemplateQuery(string EntityType) : IRequest<Result<byte[]>>;
