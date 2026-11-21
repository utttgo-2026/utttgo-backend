using UTTTGO.Api.Modules.Media.Contracts;
using UTTTGO.Api.Modules.Media.Infrastructure;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Media.Application;

public class SignedUrlService(IS3MediaAssetService s3Service, IOptions<S3Config> s3Config,
    IMediaAssetRepository repository, IAuthenticatedLocalProfileReader profiles) : ISignedUrlService
{
    private readonly IS3MediaAssetService _s3Service = s3Service;
    private readonly S3Config _s3Config = s3Config.Value;
    private readonly IMediaAssetRepository _repository = repository;
    private readonly IAuthenticatedLocalProfileReader _profiles = profiles;

    public async Task<PresignedUrlResponse> GeneratePresignedUrlAsync(string token,
        PresignedUrlRequest request, CancellationToken cancellationToken = default)
    {
        var ownerId = await MediaIdentity.ResolveOwnerIdAsync(_profiles, token, cancellationToken);

        // Validación de tipos MIME permitidos
        var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedMimeTypes.Contains(request.MimeType, StringComparer.OrdinalIgnoreCase))
        {
            throw new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("mimeType", "Tipo MIME no permitido. Use: image/jpeg, image/png, image/webp")
            });
        }

        // Validación de tamaño máximo (5 MB)
        const long maxSize = 5 * 1024 * 1024; // 5 MB
        if (request.Size > maxSize)
        {
            throw new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("size", "El tamaño máximo es 5 MB")
            });
        }

        // Generar clave única para el objeto S3
        var objectKey = $"media-assets/{request.FileName}_{Guid.NewGuid():N}.{GetExtensionFromMime(request.MimeType)}";

        var result = await _s3Service.GetPresignedPutUrlAsync(objectKey, request.MimeType, cancellationToken);

        // The asset is registered with the presigned intent, before the upload happens: the
        // owner is recorded here so Catalog can later prove the image belongs to this seller.
        await _repository.AddAsync(new MediaAssetRecord
        {
            AssetId = result.AssetId,
            ObjectKey = result.ObjectKey,
            Status = "PendingUpload",
            UserId = ownerId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return new PresignedUrlResponse
        {
            AssetId = result.AssetId,
            UploadUrl = result.UploadUrl,
            ExpiresAt = result.ExpiresAt,
            ObjectKey = result.ObjectKey
        };
    }

    private static string GetExtensionFromMime(string mimeType)
        => mimeType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin"
        };
}

public class MediaAssetService(IMediaAssetRepository repo, IOptions<S3Config> s3Config,
    IAuthenticatedLocalProfileReader profiles) : IMediaAssetService
{
    private readonly IMediaAssetRepository _repository = repo;
    private readonly S3Config _s3Config = s3Config.Value;
    private readonly IAuthenticatedLocalProfileReader _profiles = profiles;

    public async Task<ConfirmResponse> ConfirmAssetAsync(string token, ConfirmRequest request,
        CancellationToken cancellationToken = default)
    {
        var callerId = await MediaIdentity.ResolveOwnerIdAsync(_profiles, token, cancellationToken);

        var asset = await _repository.GetByIdAsync(request.AssetId, cancellationToken);
        // An asset of another user is reported as missing: existence is never disclosed.
        if (asset is null || !string.Equals(asset.UserId, callerId, StringComparison.Ordinal))
        {
            throw new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("assetId", "Asset no encontrado")
            });
        }

        if (string.Equals(asset.Status, "Rejected", StringComparison.Ordinal))
        {
            throw new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("assetId", "Asset no disponible")
            });
        }

        // Transicionar de PendingUpload a Ready (idempotente).
        if (!string.Equals(asset.Status, "Ready", StringComparison.Ordinal))
        {
            asset.Status = "Ready";
            asset.ConfirmedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(asset, cancellationToken);
        }

        // Generar URL pública
        var publicUrl = !string.IsNullOrWhiteSpace(_s3Config.PublicBaseUrl)
            ? $"{_s3Config.PublicBaseUrl.TrimEnd('/')}/{asset.ObjectKey}"
            : $"https://{_s3Config.BucketName}.s3.amazonaws.com/{asset.ObjectKey}";

        return new ConfirmResponse
        {
            PublicUrl = publicUrl,
            AssetId = asset.AssetId,
            Status = asset.Status
        };
    }
}

internal static class MediaIdentity
{
    public static async Task<string> ResolveOwnerIdAsync(IAuthenticatedLocalProfileReader profiles,
        string token, CancellationToken cancellationToken)
    {
        var authorization = await profiles.ReadAsync(token, cancellationToken);
        if (authorization.Outcome is AuthenticatedLocalProfileOutcome.Unauthorized
            or AuthenticatedLocalProfileOutcome.ProfileMissing || authorization.Profile is null)
            throw new MediaUnauthorizedException("Sesión no autorizada.");
        if (authorization.Outcome != AuthenticatedLocalProfileOutcome.Success)
            throw new InvalidOperationException("The local profile could not be read.");
        return authorization.Profile.Id;
    }
}
