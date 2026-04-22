namespace Starter.Module.Workflow.Application.Queries.GetWorkflowAnalytics;

public enum WindowSelector
{
    SevenDays = 0,
    ThirtyDays = 1,
    NinetyDays = 2,
    AllTime = 3,
}

public static class WindowSelectorParser
{
    public static bool TryParse(string? raw, out WindowSelector value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        switch (raw.Trim().ToLowerInvariant())
        {
            case "7d":  value = WindowSelector.SevenDays;  return true;
            case "30d": value = WindowSelector.ThirtyDays; return true;
            case "90d": value = WindowSelector.NinetyDays; return true;
            case "all": value = WindowSelector.AllTime;    return true;
            default: return false;
        }
    }
}
