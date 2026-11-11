namespace Ticketing.Infrastructure.Core.FileStorage;

public interface IFileStorage
{
    Task<string> GeneratePresignedUploadUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
    Task<string> GeneratePresignedDownloadUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}