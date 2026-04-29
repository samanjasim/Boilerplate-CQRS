using MediatR;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.ModelDefaults.UpsertModelDefault;

public sealed record UpsertModelDefaultCommand(
    Guid? TenantId,
    AiAgentClass AgentClass,
    AiProviderType Provider,
    string Model,
    int? MaxTokens,
    double? Temperature) : IRequest<Result<AiModelDefaultDto>>;
