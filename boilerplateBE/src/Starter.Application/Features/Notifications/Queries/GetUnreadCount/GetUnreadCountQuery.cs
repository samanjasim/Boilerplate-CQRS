using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Notifications.Queries.GetUnreadCount;

public sealed record GetUnreadCountQuery : IRequest<Result<int>>;
