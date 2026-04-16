namespace Starter.Module.AI.Infrastructure.Ingestion;

public interface IOcrService
{
    Task<string> ExtractAsync(Stream imageStream, CancellationToken ct);
}
