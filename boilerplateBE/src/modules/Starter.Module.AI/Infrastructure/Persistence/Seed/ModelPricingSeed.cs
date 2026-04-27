using Microsoft.EntityFrameworkCore;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Entities;

namespace Starter.Module.AI.Infrastructure.Persistence.Seed;

public static class ModelPricingSeed
{
    public static async Task SeedAsync(AiDbContext db, CancellationToken ct = default)
    {
        if (await db.AiModelPricings.AnyAsync(ct)) return;
        var t = DateTimeOffset.UtcNow.AddYears(-1);
        var rows = new[]
        {
            // OpenAI — 2026-04 prices
            AiModelPricing.Create(AiProviderType.OpenAI, "gpt-4o",            0.0025m, 0.01m,  t, null),
            AiModelPricing.Create(AiProviderType.OpenAI, "gpt-4o-mini",       0.00015m, 0.0006m, t, null),
            AiModelPricing.Create(AiProviderType.OpenAI, "text-embedding-3-small", 0.00002m, 0m, t, null),
            AiModelPricing.Create(AiProviderType.OpenAI, "text-embedding-3-large", 0.00013m, 0m, t, null),
            // Anthropic
            AiModelPricing.Create(AiProviderType.Anthropic, "claude-sonnet-4-6",   0.003m,  0.015m, t, null),
            AiModelPricing.Create(AiProviderType.Anthropic, "claude-opus-4-7",     0.015m,  0.075m, t, null),
            AiModelPricing.Create(AiProviderType.Anthropic, "claude-haiku-4-5",    0.0008m, 0.004m, t, null),
            // Ollama — self-hosted, $0
            AiModelPricing.Create(AiProviderType.Ollama, "llama3",      0m, 0m, t, null),
            AiModelPricing.Create(AiProviderType.Ollama, "qwen2.5",     0m, 0m, t, null),
        };
        db.AiModelPricings.AddRange(rows);
        await db.SaveChangesAsync(ct);
    }
}
