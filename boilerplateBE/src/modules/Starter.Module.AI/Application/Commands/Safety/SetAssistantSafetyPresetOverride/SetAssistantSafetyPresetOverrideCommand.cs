using MediatR;
using Starter.Abstractions.Ai;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Safety.SetAssistantSafetyPresetOverride;

public sealed record SetAssistantSafetyPresetOverrideCommand(
    Guid AssistantId,
    SafetyPreset? Preset) : IRequest<Result>;
