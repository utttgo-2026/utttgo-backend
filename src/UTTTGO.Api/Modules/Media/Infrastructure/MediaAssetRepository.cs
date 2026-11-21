using UTTTGO.Api.Modules.Media.Contracts;

namespace UTTTGO.Api.Modules.Media.Infrastructure;

public class MediaAssetRecord
{
    public string AssetId { get; set; } = Guid.NewGuid().ToString();
    public string ObjectKey { get; set; } = string.Empty;
    public string Status { get; set; } = "PendingUpload"; // PendingUpload, Ready, Rejected
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
}

public interface IMediaAssetRepository
{
    Task<MediaAssetRecord?> GetByIdAsync(string assetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaAssetRecord>> GetByIdsAsync(IReadOnlyCollection<string> assetIds,
        CancellationToken cancellationToken = default);
    Task<MediaAssetRecord?> GetByObjectKeyAsync(string objectKey, CancellationToken cancellationToken = default);
    Task<MediaAssetRecord> AddAsync(MediaAssetRecord record, CancellationToken cancellationToken = default);
    Task<MediaAssetRecord> UpdateAsync(MediaAssetRecord record, CancellationToken cancellationToken = default);
}

// Registered as a singleton: the presigned intent and the confirmation arrive in different
// requests, so the store must survive the request scope. Access is serialized because the
// shared dictionary is not thread safe.
public class InMemoryMediaAssetRepository : IMediaAssetRepository
{
    private readonly Dictionary<string, MediaAssetRecord> _store = new();
    private readonly object _lock = new();

    public async Task<MediaAssetRecord?> GetByIdAsync(string assetId, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        lock (_lock)
        {
            return _store.TryGetValue(assetId, out var record) ? record : null;
        }
    }

    public async Task<MediaAssetRecord?> GetByObjectKeyAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        lock (_lock)
        {
            return _store.Values.FirstOrDefault(r => r.ObjectKey == objectKey);
        }
    }

    public async Task<IReadOnlyList<MediaAssetRecord>> GetByIdsAsync(
        IReadOnlyCollection<string> assetIds, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        lock (_lock)
        {
            var ids = assetIds.ToHashSet(StringComparer.Ordinal);
            return _store.Values.Where(record => ids.Contains(record.AssetId)).ToList();
        }
    }

    public async Task<MediaAssetRecord> AddAsync(MediaAssetRecord record, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        lock (_lock)
        {
            _store[record.AssetId] = record;
        }
        return record;
    }

    public async Task<MediaAssetRecord> UpdateAsync(MediaAssetRecord record, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        lock (_lock)
        {
            if (_store.TryGetValue(record.AssetId, out _))
            {
                _store[record.AssetId] = record;
            }
        }
        return record;
    }
}
