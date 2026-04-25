using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.GetTemplates;

public sealed record GetTemplatesQuery : IRequest<Result<IReadOnlyList<AiAgentTemplateDto>>>;
