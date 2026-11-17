using UTTTGO.Api.Modules.Media.Contracts;

namespace UTTTGO.Api.Modules.Media.Application;

public interface ISignedUrlService
{
    Task<PresignedUrlResponse> GeneratePresignedUrlAsync(PresignedUrlRequest request, CancellationToken cancellationToken = default);
}

public interface IMediaAssetService
{
    Task<ConfirmResponse> ConfirmAssetAsync(ConfirmRequest request, CancellationToken cancellationToken = default);
}

public interface IS3MediaAssetService
{
    Task<PresignedPutUrlResult> GetPresignedPutUrlAsync(string objectKey, string contentType, CancellationToken cancellationToken = default);
    Task<string> GetPresignedGetUrlAsync(string objectKey, CancellationToken cancellationToken = default);
    Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default);
}

public sealed record PresignedPutUrlResult(
    string AssetId,
    string UploadUrl,
    DateTime ExpiresAt,
    string ObjectKey
);