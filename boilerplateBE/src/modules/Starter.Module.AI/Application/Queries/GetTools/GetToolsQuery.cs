using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetTools;

public sealed record GetToolsQuery(
    string? Category = null,
    bool? IsEnabled = null,
    string? SearchTerm = null) : IRequest<Result<IReadOnlyList<AiToolDto>>>;
