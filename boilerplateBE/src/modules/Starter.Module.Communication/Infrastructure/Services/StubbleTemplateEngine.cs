using Stubble.Core.Builders;
using Stubble.Core;

namespace Starter.Module.Communication.Infrastructure.Services;

internal sealed class StubbleTemplateEngine : ITemplateEngine
{
    private readonly StubbleVisitorRenderer _renderer;

    public StubbleTemplateEngine()
    {
        _renderer = new StubbleBuilder().Build();
    }

    public string Render(string template, Dictionary<string, object> variables)
    {
        return _renderer.Render(template, variables);
    }

    public bool Validate(string template, out string[] errors)
    {
        try
        {
            // Attempt to render with empty data to catch syntax errors
            _renderer.Render(template, new Dictionary<string, object>());
            errors = [];
            return true;
        }
        catch (Exception ex)
        {
            errors = [ex.Message];
            return false;
        }
    }
}
