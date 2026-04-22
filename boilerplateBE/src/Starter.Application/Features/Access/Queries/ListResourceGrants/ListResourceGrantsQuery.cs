using MediatR;
using Starter.Application.Common.Access.DTOs;
using Starter.Shared.Results;

namespace Starter.Application.Features.Access.Queries.ListResourceGrants;

public sealed record ListResourceGrantsQuery(
    string ResourceType,
    Guid ResourceId) : IRequest<Result<IReadOnlyList<ResourceGrantDto>>>;
