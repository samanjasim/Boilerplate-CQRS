using Amazon.S3;
using Amazon.S3.Util;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Infrastructure.Settings;

namespace Starter.Infrastructure.Services;

public sealed class StorageBucketInitializer(
    IAmazonS3 s3Client,
    IOptions<StorageSettings> settings,
    ILogger<StorageBucketInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bucketName = settings.Value.BucketName;
        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName);
            if (!exists)
            {
                await s3Client.PutBucketAsync(bucketName, cancellationToken);
                logger.LogInformation("Storage bucket '{BucketName}' created", bucketName);
            }
            else
            {
                logger.LogDebug("Storage bucket '{BucketName}' already exists", bucketName);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize storage bucket '{BucketName}'. File uploads may fail until bucket is created manually", bucketName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
