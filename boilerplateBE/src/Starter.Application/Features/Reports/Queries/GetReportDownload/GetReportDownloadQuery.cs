using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Reports.Queries.GetReportDownload;

public sealed record GetReportDownloadQuery(Guid Id) : IRequest<Result<string>>;
