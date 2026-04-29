using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Settings.ModelDefaults.GetModelDefaults;

public sealed record GetModelDefaultsQuery(Guid? TenantId) : IRequest<Result<IReadOnlyList<AiModelDefaultDto>>>;
