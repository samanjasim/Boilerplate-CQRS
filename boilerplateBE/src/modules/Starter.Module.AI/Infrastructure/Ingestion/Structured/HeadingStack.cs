namespace Starter.Module.AI.Infrastructure.Ingestion.Structured;

internal sealed class HeadingStack
{
    private readonly List<(int Level, string Text)> _frames = new();

    public string Breadcrumb => _frames.Count == 0 ? string.Empty : string.Join(" > ", _frames.Select(f => f.Text));
    public string? DeepestHeading => _frames.Count == 0 ? null : _frames[^1].Text;

    public void Push(int level, string text)
    {
        while (_frames.Count > 0 && _frames[^1].Level >= level)
            _frames.RemoveAt(_frames.Count - 1);
        _frames.Add((level, text));
    }

    public void Reset() => _frames.Clear();
}
