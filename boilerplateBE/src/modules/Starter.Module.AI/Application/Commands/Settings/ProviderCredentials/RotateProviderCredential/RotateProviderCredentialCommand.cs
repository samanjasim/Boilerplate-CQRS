using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.RotateProviderCredential;

public sealed record RotateProviderCredentialCommand(Guid Id, string Secret) : IRequest<Result<AiProviderCredentialDto>>;
