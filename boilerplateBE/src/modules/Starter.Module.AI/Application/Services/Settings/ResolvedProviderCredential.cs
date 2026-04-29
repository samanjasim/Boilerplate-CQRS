using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Application.Services.Settings;

internal sealed record ResolvedProviderCredential(
    AiProviderType Provider,
    string Secret,
    ProviderCredentialSource Source,
    Guid? ProviderCredentialId);
