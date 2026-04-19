using System.Text;
using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Module.AI.Infrastructure.Retrieval;

internal static class ContextPromptBuilder
{
    public static string Build(string assistantSystemPrompt, RetrievedContext context)
    {
        var sb = new StringBuilder();

        if (!context.IsEmpty)
        {
            sb.AppendLine("You have access to the following knowledge base excerpts, numbered [1]..[N].");
            sb.AppendLine("Ground your answer in these excerpts when they are relevant.");
            sb.AppendLine("When you use information from an excerpt, cite it inline as [n] (or [n, m] for multiple).");
            sb.AppendLine("If the excerpts do not contain the answer, say so plainly and do not fabricate citations.");
            sb.AppendLine();
            sb.AppendLine("<context>");

            var parentsById = context.Parents.ToDictionary(p => p.ChunkId);

            for (int i = 0; i < context.Children.Count; i++)
            {
                var child = context.Children[i];
                sb.Append('[').Append(i + 1).Append("] Document: \"").Append(child.DocumentName).Append('"');
                if (!string.IsNullOrWhiteSpace(child.SectionTitle))
                    sb.Append(" · Section: \"").Append(child.SectionTitle).Append('"');
                if (child.PageNumber.HasValue)
                    sb.Append(" · Page ").Append(child.PageNumber);
                sb.AppendLine();
                sb.AppendLine(child.Content);

                if (child.ParentChunkId.HasValue && parentsById.TryGetValue(child.ParentChunkId.Value, out var parent))
                {
                    sb.AppendLine("(context continues)");
                    sb.AppendLine(parent.Content);
                }
                sb.AppendLine();
            }

            sb.AppendLine("</context>");
            sb.AppendLine();
        }

        sb.AppendLine("<assistant_instructions>");
        sb.AppendLine(assistantSystemPrompt);
        sb.AppendLine("</assistant_instructions>");

        return sb.ToString();
    }
}
