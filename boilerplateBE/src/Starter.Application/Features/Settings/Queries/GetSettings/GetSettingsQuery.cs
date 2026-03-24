using Starter.Application.Features.Settings.DTOs;
using Starter.Shared.Results;
using MediatR;

namespace Starter.Application.Features.Settings.Queries.GetSettings;

public sealed record GetSettingsQuery : IRequest<Result<List<SettingGroupDto>>>;
