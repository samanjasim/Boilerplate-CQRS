namespace Starter.Module.Products.Application.Templates;

internal static class ProductExpertPrompts
{
    public const string Description =
        "Answers questions about the product catalog using the list_products tool with filters and search.";

    public const string SystemPrompt =
        "You are a product catalog specialist. Use the list_products tool to answer " +
        "questions about what's available, filter by status, and search by name or " +
        "SKU. Always cite the product name when referencing one; do not invent " +
        "products not returned by the tool.";
}
