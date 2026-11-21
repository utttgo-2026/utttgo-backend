using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Media.Infrastructure;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Media.Application;

public sealed class MediaAssetReader(IMediaAssetRepository repository, IOptions<S3Config> s3Config)
    : IMediaAssetReader
{
    private readonly IMediaAssetRepository _repository = repository;
    private readonly S3Config _s3 = s3Config.Value;

    public async Task<MediaAssetSnapshot?> FindByPublicUrlAsync(string publicUrl,
        CancellationToken cancellationToken = default)
    {
        var objectKey = ResolveObjectKey(publicUrl);
        if (objectKey is null) return null;

        var asset = await _repository.GetByObjectKeyAsync(objectKey, cancellationToken);
        if (asset is null || string.IsNullOrWhiteSpace(asset.UserId)) return null;
        return new MediaAssetSnapshot(asset.AssetId, asset.Status, asset.UserId);
    }

    // The public URL is exactly "{PublicBaseUrl}/{objectKey}"; anything outside that prefix
    // is not ours and is reported as missing instead of being guessed from the host.
    private string? ResolveObjectKey(string publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl) || string.IsNullOrWhiteSpace(_s3.PublicBaseUrl))
            return null;

        var baseUrl = _s3.PublicBaseUrl.TrimEnd('/');
        if (!publicUrl.StartsWith(baseUrl + "/", StringComparison.Ordinal)) return null;

        var objectKey = publicUrl[baseUrl.Length..].TrimStart('/');
        return objectKey.Length == 0 ? null : objectKey;
    }
}
