namespace Starter.Infrastructure.Settings;

public class AblySettings
{
    public const string SectionName = "AblySettings";
    public bool Enabled { get; set; } = false;
    public string ApiKey { get; set; } = "";
}
