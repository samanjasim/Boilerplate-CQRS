using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.ImportExport.Queries.GetImportTemplate;

public sealed record GetImportTemplateQuery(string EntityType) : IRequest<Result<byte[]>>;
