using Starter.Abstractions.Ai;
using Starter.Abstractions.Capabilities;

namespace Starter.Module.Products.Application.Templates;

public sealed class ProductExpertAnthropicTemplate : IAiAgentTemplate
{
    public string Slug => "product_expert_anthropic";
    public string DisplayName => "Product Expert (Anthropic)";
    public string Description => ProductExpertPrompts.Description;
    public string Category => "Products";
    public string SystemPrompt => ProductExpertPrompts.SystemPrompt;
    public AiProviderType Provider => AiProviderType.Anthropic;
    public string Model => "claude-sonnet-4-20250514";
    public double Temperature => 0.4;
    public int MaxTokens => 3072;
    public AssistantExecutionMode ExecutionMode => AssistantExecutionMode.Chat;
    public IReadOnlyList<string> EnabledToolNames { get; } = new[] { "list_products" };
    public IReadOnlyList<string> PersonaTargetSlugs { get; } = new[] { "default" };
    public SafetyPreset? SafetyPresetOverride => null;
}
