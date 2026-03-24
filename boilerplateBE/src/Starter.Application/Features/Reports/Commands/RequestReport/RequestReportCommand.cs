using MediatR;
using Starter.Application.Features.Reports.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Reports.Commands.RequestReport;

public sealed record RequestReportCommand(
    string ReportType,
    string Format,
    string? Filters,
    bool ForceRefresh) : IRequest<Result<ReportDto>>;
