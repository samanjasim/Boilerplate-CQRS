using MediatR;
using Starter.Application.Features.Dashboard.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Dashboard.Queries.GetDashboardAnalytics;

public sealed record GetDashboardAnalyticsQuery(string Period = "30d") : IRequest<Result<DashboardAnalyticsDto>>;
