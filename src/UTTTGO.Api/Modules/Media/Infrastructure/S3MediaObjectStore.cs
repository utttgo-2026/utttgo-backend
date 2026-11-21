using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Media.Infrastructure;

// Credentials are resolved by the AWS SDK provider chain; never from request data.
public sealed class S3MediaObjectStore(IOptions<S3Config> options) : IMediaObjectStore, IMediaImageSource
{
    private S3Config Configuration => options.Value;
    private (AmazonS3Client Client, string Bucket) Open()
    {
        var config = Configuration;
        if (string.IsNullOrWhiteSpace(config.BucketName))
            throw new InvalidOperationException("Media bucket is not configured.");
        return (new AmazonS3Client(RegionEndpoint.GetBySystemName(config.Region)), config.BucketName);
    }

    public Task<(string Url, DateTime ExpiresAt, IReadOnlyDictionary<string, string> Headers)> SignPutAsync(
        string key, string mime, CancellationToken cancellationToken)
    {
        var (client, bucket) = Open();
        using (client)
        {
            var expires = DateTime.UtcNow.AddMinutes(5);
            var url = client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key = key,
                Verb = HttpVerb.PUT,
                ContentType = mime,
                Expires = expires
            });
            return Task.FromResult<(string, DateTime, IReadOnlyDictionary<string, string>)>(
                (url, expires, new Dictionary<string, string> { ["Content-Type"] = mime }));
        }
    }

    public async Task<(long Length, string Mime)?> HeadAsync(string key, CancellationToken cancellationToken)
    {
        var (client, bucket) = Open();
        using (client)
        {
            try
            {
                var response = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = bucket,
                    Key = key
                }, cancellationToken);
                return (response.ContentLength, response.Headers.ContentType ?? "");
            }
            catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }
    }

    public Task<MediaReadUrl> SignGetAsync(string key, CancellationToken cancellationToken)
    {
        var (client, bucket) = Open();
        using (client)
        {
            var expires = DateTime.UtcNow.AddMinutes(5);
            return Task.FromResult(new MediaReadUrl(client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = expires
            }), expires));
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var (client, bucket) = Open();
        using (client)
            await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key },
                cancellationToken);
    }

    public async Task<byte[]> ReadForModerationAsync(string objectKey, string expectedMime, long expectedSize,
        CancellationToken cancellationToken)
    {
        var (client, bucket) = Open();
        using (client)
        using (var response = await client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucket,
            Key = objectKey
        }, cancellationToken))
        {
            if (response.ContentLength != expectedSize || response.ContentLength is <= 0 or > MediaImagePolicy.MaxBytes ||
                !string.Equals(response.Headers.ContentType, expectedMime, StringComparison.OrdinalIgnoreCase))
                throw new InvalidMediaImageException();
            await using var result = new MemoryStream();
            var buffer = new byte[81920];
            int length;
            while ((length = await response.ResponseStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (result.Length + length > MediaImagePolicy.MaxBytes)
                    throw new InvalidMediaImageException();
                await result.WriteAsync(buffer.AsMemory(0, length), cancellationToken);
            }
            if (result.Length != response.ContentLength)
                throw new InvalidMediaImageException();
            return result.ToArray();
        }
    }
}
