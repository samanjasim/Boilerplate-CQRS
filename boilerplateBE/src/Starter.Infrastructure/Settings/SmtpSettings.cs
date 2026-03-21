namespace Starter.Infrastructure.Settings;

public class SmtpSettings
{
    public const string SectionName = "SmtpSettings";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = "noreply@starter.com";
    public string FromName { get; set; } = "Starter";
    public bool EnableSsl { get; set; } = true;
}
