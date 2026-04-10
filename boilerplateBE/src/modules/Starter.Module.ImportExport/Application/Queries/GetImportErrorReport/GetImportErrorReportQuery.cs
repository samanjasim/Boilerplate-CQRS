using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.ImportExport.Application.Queries.GetImportErrorReport;

public sealed record GetImportErrorReportQuery(Guid Id) : IRequest<Result<string>>;
