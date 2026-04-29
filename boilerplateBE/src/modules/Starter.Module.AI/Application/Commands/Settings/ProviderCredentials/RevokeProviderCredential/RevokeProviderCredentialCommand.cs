using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.RevokeProviderCredential;

public sealed record RevokeProviderCredentialCommand(Guid Id) : IRequest<Result>;
