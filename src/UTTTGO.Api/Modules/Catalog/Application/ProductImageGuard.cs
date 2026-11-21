using UTTTGO.Api.Modules.Media.Application;

namespace UTTTGO.Api.Modules.Catalog.Application;

// Decides whether an imageUrl may be attached to a product. Catalog never reads the Media
// collections: it only goes through IMediaAssetReader, the read contract Media owns.
public interface IProductImageGuard
{
    Task<MediaAssetSnapshot?> ResolveAttachableAsync(string reference, string ownerUserId,
        CancellationToken cancellationToken);
    Task<string?> GetReadUrlAsync(MediaAssetReference? reference, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, string>> GetReadUrlsAsync(
        IReadOnlyCollection<MediaAssetReference> references, CancellationToken cancellationToken);
}

public sealed class ProductImageGuard(IMediaAssetReader assets)
    : IProductImageGuard
{
    private readonly IMediaAssetReader _assets = assets;

    public async Task<MediaAssetSnapshot?> ResolveAttachableAsync(string reference, string ownerUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(ownerUserId)) return null;
        var asset = await _assets.FindByReferenceAsync(reference, cancellationToken);
        return asset is not null
            && asset.Status is "Ready" or "Approved"
            && string.Equals(asset.OwnerUserId, ownerUserId, StringComparison.Ordinal)
            ? asset : null;
    }

    public Task<string?> GetReadUrlAsync(MediaAssetReference? reference,
        CancellationToken cancellationToken) => _assets.GetReadUrlAsync(reference, cancellationToken);

    public Task<IReadOnlyDictionary<string, string>> GetReadUrlsAsync(
        IReadOnlyCollection<MediaAssetReference> references, CancellationToken cancellationToken) =>
        _assets.GetReadUrlsAsync(references, cancellationToken);
}
