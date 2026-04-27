using Starter.Abstractions.Ai;
using Starter.Shared.Results;

namespace Starter.Module.AI.Domain.Errors;

public static class AiAgentErrors
{
    public static Error AgentPrincipalNotFound(Guid id) =>
        Error.NotFound("AiAgent.PrincipalNotFound", $"Agent principal '{id}' not found.");

    public static Error CostCapExceeded(string tier, decimal capUsd, decimal currentUsd) =>
        new("AiAgent.CostCapExceeded",
            $"Cost cap exceeded at {tier} tier (cap: ${capUsd:F2}, current: ${currentUsd:F2}).",
            ErrorType.TooManyRequests);

    public static Error RateLimitExceeded(int rpm) =>
        new("AiAgent.RateLimitExceeded", $"Rate limit of {rpm} requests per minute exceeded.",
            ErrorType.TooManyRequests);

    public static Error PricingMissing(AiProviderType provider, string model) =>
        Error.Failure("AiAgent.PricingMissing", $"No pricing configured for {provider}/{model}.");

    public static Error AgentRoleAssignmentNotPermitted(Guid roleId) =>
        new("AiAgent.AgentRoleAssignmentNotPermitted",
            $"Role '{roleId}' cannot be assigned to agent principals.",
            ErrorType.Forbidden);

    public static Error AgentMaxCountExceeded(int max, int current) =>
        new("AiAgent.MaxCountExceeded",
            $"Plan permits at most {max} AI agents (currently {current}). Upgrade your plan to add more.",
            ErrorType.Validation);

    public static Error OperationalAgentsNotEnabled() =>
        new("AiAgent.OperationalNotEnabled",
            "Operational AI agents (event/cron-triggered) are not enabled on the current plan.",
            ErrorType.Validation);
}
