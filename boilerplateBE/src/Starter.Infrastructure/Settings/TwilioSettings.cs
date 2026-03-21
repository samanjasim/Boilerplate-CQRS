namespace Starter.Infrastructure.Settings;

public class TwilioSettings
{
    public const string SectionName = "TwilioSettings";

    public bool Enabled { get; set; } = false;
    public string AccountSid { get; set; } = "";
    public string AuthToken { get; set; } = "";
    public string FromNumber { get; set; } = "";
}
