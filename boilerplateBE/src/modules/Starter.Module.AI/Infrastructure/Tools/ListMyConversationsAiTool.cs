using System.Text.Json;
using Starter.Abstractions.Capabilities;
using Starter.Module.AI.Application.Queries.GetConversations;
using Starter.Module.AI.Constants;

namespace Starter.Module.AI.Infrastructure.Tools;

internal sealed class ListMyConversationsAiTool : IAiToolDefinition
{
    private static readonly JsonElement Schema = JsonDocument.Parse("""
    {
      "type": "object",
      "properties": {
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many recent conversations to return (default 10)."
        }
      },
      "additionalProperties": false
    }
    """).RootElement;

    public string Name => "list_my_conversations";
    public string Description =>
        "List the current user's recent AI conversations with title, message count, and last-message timestamp.";
    public JsonElement ParameterSchema => Schema;
    public Type CommandType => typeof(GetConversationsQuery);
    public string RequiredPermission => AiPermissions.ViewConversations;
    public string Category => "AI";
    public bool IsReadOnly => true;
}
