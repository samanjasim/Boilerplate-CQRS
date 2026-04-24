namespace Starter.Module.AI.Application.Services.Personas;

internal interface ISlugGenerator
{
    string Slugify(string input);
    string EnsureUnique(string slug, ISet<string> taken);
}
