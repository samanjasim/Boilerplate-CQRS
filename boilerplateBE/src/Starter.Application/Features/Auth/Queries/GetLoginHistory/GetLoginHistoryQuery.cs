using Starter.Application.Common.Models;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Queries.GetLoginHistory;

public sealed record GetLoginHistoryQuery : PaginationQuery, IRequest<Result<PaginatedList<LoginHistoryDto>>>;
