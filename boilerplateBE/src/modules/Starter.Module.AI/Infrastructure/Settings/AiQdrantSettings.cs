namespace Starter.Module.AI.Infrastructure.Settings;

public sealed class AiQdrantSettings
{
    public const string SectionName = "AI:Qdrant";

    public string Host { get; init; } = "localhost";
    public int GrpcPort { get; init; } = 6334;
    public int HttpPort { get; init; } = 6333;
    public string? ApiKey { get; init; }
    public bool UseTls { get; init; } = false;
}
