using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Starter.Application.Common.Interfaces;
using Starter.Infrastructure.Settings;

namespace Starter.Infrastructure.Services;

public sealed class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly StorageSettings _settings;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(IAmazonS3 s3Client, IOptions<StorageSettings> settings, ILogger<S3StorageService> logger)
    {
        _s3Client = s3Client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> UploadAsync(Stream stream, string key, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType
        };
        await _s3Client.PutObjectAsync(request, ct);
        _logger.LogInformation("Uploaded file: {Key}", key);
        return key;
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var response = await _s3Client.GetObjectAsync(_settings.BucketName, key, ct);
        return response.ResponseStream;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _s3Client.DeleteObjectAsync(_settings.BucketName, key, ct);
        _logger.LogInformation("Deleted file: {Key}", key);
    }

    public Task<string> GetSignedUrlAsync(string key, TimeSpan expiration, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiration),
            Verb = HttpVerb.GET,
            Protocol = new Uri(_settings.Endpoint).Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
                ? Protocol.HTTPS
                : Protocol.HTTP
        };
        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public Task<string> GetPublicUrlAsync(string key, CancellationToken ct = default)
    {
        var endpoint = _settings.Endpoint.TrimEnd('/');
        var bucket = _settings.BucketName;
        var url = _settings.ForcePathStyle
            ? $"{endpoint}/{bucket}/{key}"
            : $"{endpoint}/{key}";
        return Task.FromResult(url);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(_settings.BucketName, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
