namespace UTTTGO.Api.Shared.Contracts;

public class S3Config
{
    public string BucketName { get; set; } = string.Empty;
    public int PresignedUrlExpirationMinutes { get; set; } = 15;
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-2";
    public bool EnableSSL { get; set; } = true;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}