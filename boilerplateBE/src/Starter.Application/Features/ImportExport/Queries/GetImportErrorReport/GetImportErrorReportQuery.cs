using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.ImportExport.Queries.GetImportErrorReport;

public sealed record GetImportErrorReportQuery(Guid Id) : IRequest<Result<string>>;
