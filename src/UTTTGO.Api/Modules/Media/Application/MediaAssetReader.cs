using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Media.Infrastructure;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Media.Application;

public sealed class MediaAssetReader(IMediaAssetRepository repository, IS3MediaAssetService objects,
    IOptions<S3Config> s3Config)
    : IMediaAssetReader
{
    private readonly IMediaAssetRepository _repository = repository;
    private readonly IS3MediaAssetService _objects = objects;
    private readonly S3Config _s3 = s3Config.Value;

    public async Task<MediaAssetSnapshot?> FindByReferenceAsync(string reference,
        CancellationToken cancellationToken = default)
    {
        var asset = await FindAssetAsync(reference, cancellationToken);
        return ToSnapshot(asset);
    }

    public async Task<string?> GetReadUrlAsync(MediaAssetReference? reference,
        CancellationToken cancellationToken = default)
    {
        if (reference is null) return null;
        var assets = await _repository.GetByIdsAsync([reference.AssetId], cancellationToken);
        var asset = assets.SingleOrDefault();
        return IsReadable(asset, reference) ? await _objects.GetPresignedGetUrlAsync(
            asset!.ObjectKey, cancellationToken) : null;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetReadUrlsAsync(
        IReadOnlyCollection<MediaAssetReference> references, CancellationToken cancellationToken = default)
    {
        if (references.Count == 0) return new Dictionary<string, string>();
        var assets = await _repository.GetByIdsAsync(
            references.Select(reference => reference.AssetId).ToArray(), cancellationToken);
        var byId = assets.ToDictionary(asset => asset.AssetId, StringComparer.Ordinal);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var reference in references)
        {
            if (!byId.TryGetValue(reference.AssetId, out var asset) || !IsReadable(asset, reference))
                continue;
            result[reference.AssetId] = await _objects.GetPresignedGetUrlAsync(
                asset.ObjectKey, cancellationToken);
        }

        return result;
    }

    // The public URL is exactly "{PublicBaseUrl}/{objectKey}"; anything outside that prefix
    // is not ours and is reported as missing instead of being guessed from the host.
    private async Task<MediaAssetRecord?> FindAssetAsync(string reference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var objectKey = ResolveObjectKey(reference);
        return objectKey is not null
            ? await _repository.GetByObjectKeyAsync(objectKey, cancellationToken)
            : await _repository.GetByIdAsync(reference, cancellationToken);
    }

    private string? ResolveObjectKey(string reference)
    {
        if (string.IsNullOrWhiteSpace(_s3.PublicBaseUrl)) return null;
        var baseUrl = _s3.PublicBaseUrl.TrimEnd('/');
        if (!reference.StartsWith(baseUrl + "/", StringComparison.Ordinal)) return null;

        var objectKey = reference[baseUrl.Length..].TrimStart('/');
        return objectKey.StartsWith("staging/", StringComparison.Ordinal) ? objectKey : null;
    }

    private static MediaAssetSnapshot? ToSnapshot(MediaAssetRecord? asset) =>
        asset is { UserId: not null } && asset.ObjectKey.StartsWith("staging/", StringComparison.Ordinal)
            ? new(asset.AssetId, asset.Status, asset.UserId, asset.ObjectKey) : null;

    private static bool IsReadable(MediaAssetRecord? asset, MediaAssetReference reference) =>
        asset is not null && asset.AssetId == reference.AssetId && asset.ObjectKey == reference.ObjectKey &&
        asset.ObjectKey.StartsWith("staging/", StringComparison.Ordinal) &&
        asset.Status is "Ready" or "Approved";
}
