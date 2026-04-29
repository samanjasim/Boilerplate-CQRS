using MediatR;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.CreateProviderCredential;

public sealed record CreateProviderCredentialCommand(
    Guid? TenantId,
    AiProviderType Provider,
    string DisplayName,
    string Secret) : IRequest<Result<AiProviderCredentialDto>>;
