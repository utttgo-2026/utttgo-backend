namespace UTTTGO.Api.Modules.Media.Application;

// Read-only contract owned by Media. Other modules (Catalog) never touch the Media
// collections: they ask this interface and receive only the facts they need to decide.
public sealed record MediaAssetReference(string AssetId, string ObjectKey);

public sealed record MediaAssetSnapshot(string AssetId, string Status, string OwnerUserId,
    string ObjectKey);

public interface IMediaAssetReader
{
    // Accepts either an asset ID or the canonical URL returned by media confirmation.
    Task<MediaAssetSnapshot?> FindByReferenceAsync(string reference,
        CancellationToken cancellationToken = default);

    Task<string?> GetReadUrlAsync(MediaAssetReference? reference,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetReadUrlsAsync(
        IReadOnlyCollection<MediaAssetReference> references, CancellationToken cancellationToken = default);
}
