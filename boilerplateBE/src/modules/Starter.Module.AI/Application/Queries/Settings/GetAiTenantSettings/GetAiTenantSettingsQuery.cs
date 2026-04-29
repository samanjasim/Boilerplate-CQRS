using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Settings.GetAiTenantSettings;

public sealed record GetAiTenantSettingsQuery(Guid? TenantId) : IRequest<Result<AiTenantSettingsDto>>;
