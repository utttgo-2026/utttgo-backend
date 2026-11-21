using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Catalog.Application;

// Decides whether an imageUrl may be attached to a product. Catalog never reads the Media
// collections: it only goes through IMediaAssetReader, the read contract Media owns.
public interface IProductImageGuard
{
    Task<bool> IsAttachableAsync(string imageUrl, string ownerUserId, CancellationToken cancellationToken);
}

public sealed class ProductImageGuard(IMediaAssetReader assets, IOptions<S3Config> s3Config)
    : IProductImageGuard
{
    private readonly IMediaAssetReader _assets = assets;
    private readonly S3Config _s3 = s3Config.Value;

    public async Task<bool> IsAttachableAsync(string imageUrl, string ownerUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(ownerUserId)) return false;
        if (string.IsNullOrWhiteSpace(_s3.PublicBaseUrl)) return false;
        if (!Uri.TryCreate(_s3.PublicBaseUrl, UriKind.Absolute, out var baseUrl)) return false;
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var image)) return false;
        if (!string.Equals(image.Scheme, baseUrl.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(image.Host, baseUrl.Host, StringComparison.OrdinalIgnoreCase))
            return false;

        var asset = await _assets.FindByPublicUrlAsync(imageUrl, cancellationToken);
        return asset is not null
            && string.Equals(asset.Status, "Ready", StringComparison.Ordinal)
            && string.Equals(asset.OwnerUserId, ownerUserId, StringComparison.Ordinal);
    }
}
