using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Media.Contracts;

public sealed class PresignedUrlRequest
{
    [JsonRequired] public string FileName { get; init; } = "";
    [JsonRequired] public string MimeType { get; init; } = "";
    [JsonRequired] public long Size { get; init; }
}

public sealed record PresignedUrlResponse(string MediaAssetId, string UploadUrl, DateTime ExpiresAt,
    IReadOnlyDictionary<string, string> Headers);
public sealed record ConfirmRequest(string MediaAssetId);
public sealed record ConfirmResponse(string MediaAssetId, string Status);
public sealed record MediaAssetResponse(string MediaAssetId, string Status, string? ImageUrl,
    DateTime? ImageUrlExpiresAt);
