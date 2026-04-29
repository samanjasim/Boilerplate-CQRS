using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Queries.Settings.ProviderCredentials.GetProviderCredentials;

public sealed record GetProviderCredentialsQuery(Guid? TenantId) : IRequest<Result<IReadOnlyList<AiProviderCredentialDto>>>;
