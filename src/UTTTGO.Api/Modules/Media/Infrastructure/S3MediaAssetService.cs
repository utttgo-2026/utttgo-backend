using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Media.Application;
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

        var s3Client = CreateS3Client();

        var url = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _config.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.AddMinutes(_config.PresignedUrlExpirationMinutes)
        });

        return new PresignedPutUrlResult(
            AssetId: Guid.NewGuid().ToString(),
            UploadUrl: url,
            ExpiresAt: DateTime.UtcNow.AddMinutes(_config.PresignedUrlExpirationMinutes),
            ObjectKey: objectKey
        );
    }

    public async Task<string> GetPresignedGetUrlAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var s3Client = CreateS3Client();

        var url = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _config.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(_config.PresignedUrlExpirationMinutes)
        });

        return url;
    }

    public async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var s3Client = CreateS3Client();

        await s3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _config.BucketName,
            Key = objectKey
        }, cancellationToken);
    }

    private AmazonS3Client CreateS3Client()
    {
        var credentials = new Amazon.Runtime.BasicAWSCredentials(_config.AccessKey, _config.SecretKey);
        var config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_config.Region),
            ForcePathStyle = !_config.EnableSSL
        };
        return new AmazonS3Client(credentials, config);
    }
}