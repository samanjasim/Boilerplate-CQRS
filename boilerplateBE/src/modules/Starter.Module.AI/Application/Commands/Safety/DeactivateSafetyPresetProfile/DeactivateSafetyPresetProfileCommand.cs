using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Safety.DeactivateSafetyPresetProfile;

public sealed record DeactivateSafetyPresetProfileCommand(Guid ProfileId) : IRequest<Result>;
