using MediatR;
using Starter.Module.Workflow.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Queries.GetInboxStatusCounts;

public sealed record GetInboxStatusCountsQuery : IRequest<Result<InboxStatusCountsDto>>;
