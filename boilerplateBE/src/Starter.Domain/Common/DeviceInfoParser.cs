namespace Starter.Domain.Common;

public static class DeviceInfoParser
{
    public static string? Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return null;

        var browser = "Unknown Browser";
        var os = "Unknown OS";

        // Detect browser (order matters: check Edge/Opera before Chrome)
        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
            browser = "Edge";
        else if (userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase) ||
                 userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase))
            browser = "Opera";
        else if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase))
            browser = "Chrome";
        else if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
            browser = "Firefox";
        else if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase) &&
                 !userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase))
            browser = "Safari";

        // Detect OS
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            os = "Windows";
        else if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) ||
                 userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase))
            os = "macOS";
        else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
            os = "Android";
        else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
                 userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            os = "iOS";
        else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            os = "Linux";

        return $"{browser} on {os}";
    }
}
