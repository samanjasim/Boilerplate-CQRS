using MediatR;
using Starter.Shared.Results;

namespace Starter.Application.Features.Reports.Commands.DeleteReport;

public sealed record DeleteReportCommand(Guid Id) : IRequest<Result>;
