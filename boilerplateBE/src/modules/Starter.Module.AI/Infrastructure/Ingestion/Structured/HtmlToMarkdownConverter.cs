using ReverseMarkdown;

namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal sealed class HtmlToMarkdownConverter
{
    private readonly Converter _converter = new(new Config
    {
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true,
    });

    public string Convert(string html) =>
        string.IsNullOrWhiteSpace(html) ? string.Empty : _converter.Convert(html);
}
