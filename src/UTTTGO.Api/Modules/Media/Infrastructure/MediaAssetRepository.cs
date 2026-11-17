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
    Task<MediaAssetRecord?> GetByObjectKeyAsync(string objectKey, CancellationToken cancellationToken = default);
    Task<MediaAssetRecord> AddAsync(MediaAssetRecord record, CancellationToken cancellationToken = default);
    Task<MediaAssetRecord> UpdateAsync(MediaAssetRecord record, CancellationToken cancellationToken = default);
}

public class InMemoryMediaAssetRepository : IMediaAssetRepository
{
    private readonly Dictionary<string, MediaAssetRecord> _store = new();
    private readonly object _lock = new();

    public async Task<MediaAssetRecord?> GetByIdAsync(string assetId, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return _store.TryGetValue(assetId, out var record) ? record : null;
    }

    public async Task<MediaAssetRecord?> GetByObjectKeyAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return _store.Values.FirstOrDefault(r => r.ObjectKey == objectKey);
    }

    public async Task<MediaAssetRecord> AddAsync(MediaAssetRecord record, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        _store[record.AssetId] = record;
        return record;
    }

    public async Task<MediaAssetRecord> UpdateAsync(MediaAssetRecord record, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        if (_store.TryGetValue(record.AssetId, out var existing))
        {
            _store[record.AssetId] = record;
        }
        return record;
    }
}