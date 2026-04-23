using Starter.Abstractions.Paging;
using Starter.Application.Common.Models;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Auth.Queries.GetInvitations;

public sealed record GetInvitationsQuery : PaginationQuery, IRequest<Result<PaginatedList<InvitationDto>>>;
