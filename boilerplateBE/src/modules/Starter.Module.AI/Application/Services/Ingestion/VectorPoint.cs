namespace Starter.Module.AI.Application.Services.Ingestion;

public sealed record VectorPoint(Guid Id, float[] Vector, VectorPayload Payload);
