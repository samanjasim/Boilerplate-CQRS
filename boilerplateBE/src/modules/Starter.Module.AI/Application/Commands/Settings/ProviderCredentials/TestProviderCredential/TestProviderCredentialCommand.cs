using MediatR;
using Starter.Module.AI.Application.DTOs;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.TestProviderCredential;

public sealed record TestProviderCredentialCommand(Guid Id) : IRequest<Result<AiProviderCredentialDto>>;
