namespace Starter.Module.Communication.Infrastructure.Services;

public interface ITemplateEngine
{
    string Render(string template, Dictionary<string, object> variables);
    bool Validate(string template, out string[] errors);
}
