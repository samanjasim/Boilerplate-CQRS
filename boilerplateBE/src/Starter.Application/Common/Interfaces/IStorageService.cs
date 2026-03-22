namespace Starter.Application.Common.Interfaces;

public interface IStorageService
{
    Task<string> UploadAsync(Stream stream, string key, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<string> GetSignedUrlAsync(string key, TimeSpan expiration, CancellationToken ct = default);
    Task<string> GetPublicUrlAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
