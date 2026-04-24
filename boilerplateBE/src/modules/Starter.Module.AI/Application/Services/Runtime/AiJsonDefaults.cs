using System.Text.Json;
using System.Text.Json.Serialization;

namespace Starter.Module.AI.Application.Services.Runtime;

/// <summary>
/// Shared JsonSerializerOptions used by the agent runtime + tool dispatcher + chat sink.
/// Kept in one place so the JSON shape the provider/model sees on tool results + the
/// serialised shape of intermediate persistence stays consistent as the module evolves.
/// </summary>
internal static class AiJsonDefaults
{
    public static readonly JsonSerializerOptions Serializer = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Mirrors AiToolSchemaGenerator.SchemaOptions so enum-typed parameters sent by the
        // LLM as strings (e.g. "status": "Active") deserialize correctly into the handler
        // request. Keep these two option sets in lockstep.
        Converters = { new JsonStringEnumConverter() },
    };
}
