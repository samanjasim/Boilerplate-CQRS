using Starter.Abstractions.Capabilities;

namespace Starter.Module.AI.Application.Services;

public interface IAiAgentTemplateRegistry
{
    IReadOnlyCollection<IAiAgentTemplate> GetAll();
    IAiAgentTemplate? Find(string slug);
}
