namespace Starter.Application.Common.Constants;

public static class FileSettings
{
    public const string TempFileTtlMinutesKey = "Files.TempFileTtlMinutes";
    public const int TempFileTtlMinutesDefault = 120;

    public const string OrphanCleanupIntervalMinutesKey = "Files.OrphanCleanupIntervalMinutes";
    public const int OrphanCleanupIntervalMinutesDefault = 30;

    public const string MaxUploadSizeMbKey = "Files.MaxUploadSizeMb";
    public const int MaxUploadSizeMbDefault = 50;

    public const string ReportExpirationHoursKey = "Reports.FileExpirationHours";
    public const int ReportExpirationHoursDefault = 24;
}
