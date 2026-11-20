namespace UTTTGO.Api.Modules.Media.Application;

public static class MediaStatus
{
    public const string PendingUpload = "PendingUpload";
    public const string PendingModeration = "PendingModeration";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string ProcessingFailed = "ProcessingFailed";
}

public sealed record MediaRecord(string Id, string OwnerUserId, string ObjectKey, string MimeType,
    long Size, string Status, DateTime CreatedAt, DateTime? ConfirmedAt = null, long Version = 0);

public interface IMediaRepository
{
    Task AddAsync(MediaRecord record, CancellationToken cancellationToken);
    Task<MediaRecord?> FindAsync(string id, CancellationToken cancellationToken);
    Task<bool> TransitionAsync(string id, string ownerId, long version, string from, string to,
        CancellationToken cancellationToken);
}

public sealed record MediaReadUrl(string Url, DateTime ExpiresAt);

// Owned by Media. Catalog must not access mediaAssets or construct S3 URLs.
public interface IProductMediaAccess
{
    Task<bool> IsApprovedForOwnerAsync(string mediaAssetId, string ownerUserId,
        CancellationToken cancellationToken);
    Task<MediaReadUrl?> GetApprovedReadUrlAsync(string mediaAssetId, CancellationToken cancellationToken);
}

public interface IMediaObjectStore
{
    Task<(string Url, DateTime ExpiresAt, IReadOnlyDictionary<string, string> Headers)> SignPutAsync(
        string key, string mime, CancellationToken cancellationToken);
    Task<(long Length, string Mime)?> HeadAsync(string key, CancellationToken cancellationToken);
    Task<MediaReadUrl> SignGetAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

public interface IImageModerator
{
    // No configured provider or provider error must never yield true.
    Task<bool> IsSafeAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed class UnconfiguredImageModerator : IImageModerator
{
    public Task<bool> IsSafeAsync(string objectKey, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Content Safety is not configured.");
}
