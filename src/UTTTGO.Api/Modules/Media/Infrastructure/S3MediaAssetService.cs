using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Media.Infrastructure;

public class S3MediaAssetService(IOptions<S3Config> s3Config) : IS3MediaAssetService
{
    private readonly S3Config _config = s3Config.Value;

    public async Task<PresignedPutUrlResult> GetPresignedPutUrlAsync(string objectKey, string contentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("El nombre de objeto S3 es obligatorio", nameof(objectKey));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("El content-type es obligatorio", nameof(contentType));

        using var s3Client = new AmazonS3Client(_config.Credentials, _config.Config);

        var request = new PutObjectRequest
        {
            BucketName = _config.BucketName,
            Key = objectKey,
            ContentType = contentType,
            Metadata = new Dictionary<string, string>
            {
                ["uploadedAt"] = DateTime.UtcNow.ToString("o")
            }
        };

        var response = await s3Client.PutObjectAsync(request, cancellationToken);
        var url = s3Client.GetPreSignedURL(new PutObjectRequest
        {
            BucketName = _config.BucketName,
            Key = objectKey,
            ContentType = contentType
        }, DateTime.UtcNow.AddMinutes(_config.PresignedUrlExpirationMinutes));

        return new PresignedPutUrlResult
        {
            AssetId = Guid.NewGuid().ToString(),
            UploadUrl = url,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_config.PresignedUrlExpirationMinutes),
            ObjectKey = objectKey
        };
    }

    public async Task<string> GetPresignedGetUrlAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        using var s3Client = new AmazonS3Client(_config.Credentials, _config.Config);

        var request = new GetObjectRequest
        {
            BucketName = _config.BucketName,
            Key = objectKey
        };

        var response = await s3Client.GetObjectAsync(request, cancellationToken);
        return response.HttpResponseMetadata.Headers?.GetValues("ETag")?[0]
            ?? s3Client.GetPreSignedURL(new GetObjectRequest
            {
                BucketName = _config.BucketName,
                Key = objectKey
            }, DateTime.UtcNow);
    }

    public async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        using var s3Client = new AmazonS3Client(_config.Credentials, _config.Config);
        await s3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _config.BucketName,
            Key = objectKey
        }, cancellationToken);
    }
}