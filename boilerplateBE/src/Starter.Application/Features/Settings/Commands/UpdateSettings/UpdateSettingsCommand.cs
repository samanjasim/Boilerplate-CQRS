using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Settings.Commands.UpdateSettings;

public sealed record SettingUpdate(string Key, string Value);

public sealed record UpdateSettingsCommand(List<SettingUpdate> Settings) : IRequest<Result>;
