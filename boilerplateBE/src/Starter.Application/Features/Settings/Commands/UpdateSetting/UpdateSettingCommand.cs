using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Settings.Commands.UpdateSetting;

public sealed record UpdateSettingCommand(string Key, string Value) : IRequest<Result>;
