using MediatR;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Safety.UpsertSafetyPresetProfile;

public sealed record UpsertSafetyPresetProfileCommand(
    Guid? TenantId,
    SafetyPreset Preset,
    ModerationProvider Provider,
    string CategoryThresholdsJson,
    string BlockedCategoriesJson,
    ModerationFailureMode FailureMode,
    bool RedactPii) : IRequest<Result<Guid>>;
