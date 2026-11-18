using UTTTGO.Api.Modules.Media.Contracts;
using UTTTGO.Api.Modules.Media.Infrastructure;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Media.Application;

public class SignedUrlService(IS3MediaAssetService s3Service, IOptions<S3Config> s3Config) : ISignedUrlService
{
    private readonly IS3MediaAssetService _s3Service = s3Service;
    private readonly S3Config _s3Config = s3Config.Value;

    public async Task<PresignedUrlResponse> GeneratePresignedUrlAsync(PresignedUrlRequest request, CancellationToken cancellationToken = default)
    {
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

public class MediaAssetService(IMediaAssetRepository repo, IOptions<S3Config> s3Config) : IMediaAssetService
{
    private readonly IMediaAssetRepository _repository = repo;
    private readonly S3Config _s3Config = s3Config.Value;

    public async Task<ConfirmResponse> ConfirmAssetAsync(ConfirmRequest request, CancellationToken cancellationToken = default)
    {
        var asset = await _repository.GetByIdAsync(request.AssetId, cancellationToken);
        if (asset is null)
        {
            throw new FluentValidation.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("assetId", "Asset no encontrado")
            });
        }

        // Validar que el asset pertenezca al usuario autenticado (mismo principio que otros módulos)
        // En un escenario real, se compararía con el profile.Id del usuario autenticado

        // Transicionar de PendingUpload a Ready
        asset.Status = "Ready";
        asset.ConfirmedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(asset, cancellationToken);

        // Generar URL pública
        var publicUrl = !string.IsNullOrWhiteSpace(_s3Config.PublicBaseUrl)
            ? $"{_s3Config.PublicBaseUrl}/{asset.ObjectKey}"
            : $"https://{_s3Config.BucketName}.s3.amazonaws.com/{asset.ObjectKey}";

        return new ConfirmResponse
        {
            PublicUrl = publicUrl,
            AssetId = asset.AssetId,
            Status = asset.Status
        };
    }
}