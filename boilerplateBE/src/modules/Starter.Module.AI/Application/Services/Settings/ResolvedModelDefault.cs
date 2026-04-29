using Starter.Abstractions.Ai;

namespace Starter.Module.AI.Application.Services.Settings;

internal sealed record ResolvedModelDefault(
    AiProviderType Provider,
    string Model,
    double Temperature,
    int MaxTokens);
