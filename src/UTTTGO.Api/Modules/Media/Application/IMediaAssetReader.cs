namespace UTTTGO.Api.Modules.Media.Application;

// Read-only contract owned by Media. Other modules (Catalog) never touch the Media
// collections: they ask this interface and receive only the facts they need to decide.
public sealed record MediaAssetSnapshot(string AssetId, string Status, string OwnerUserId);

public interface IMediaAssetReader
{
    // Returns null when the URL is not one of ours or when no asset was uploaded under it.
    Task<MediaAssetSnapshot?> FindByPublicUrlAsync(string publicUrl,
        CancellationToken cancellationToken = default);
}
