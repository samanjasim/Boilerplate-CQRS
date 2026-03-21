namespace Starter.Domain.Exceptions;

public class BusinessRuleException : DomainException
{
    public string RuleName { get; }

    public BusinessRuleException(string ruleName, string message)
        : base(message, "BUSINESS_RULE_VIOLATION")
    {
        RuleName = ruleName;
    }
}

public interface IBusinessRule
{
    string RuleName { get; }
    string Message { get; }
    bool IsBroken();
}

public static class BusinessRuleExtensions
{
    public static void CheckRule(IBusinessRule rule)
    {
        if (rule.IsBroken())
        {
            throw new BusinessRuleException(rule.RuleName, rule.Message);
        }
    }
}
