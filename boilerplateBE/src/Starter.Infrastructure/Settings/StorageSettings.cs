namespace Starter.Infrastructure.Settings;

public class StorageSettings
{
    public const string SectionName = "StorageSettings";
    public string Provider { get; set; } = "minio";
    public string BucketName { get; set; } = "starter-files";
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string Region { get; set; } = "us-east-1";
    public bool ForcePathStyle { get; set; } = true;
    public int SignedUrlExpirationMinutes { get; set; } = 60;
}
