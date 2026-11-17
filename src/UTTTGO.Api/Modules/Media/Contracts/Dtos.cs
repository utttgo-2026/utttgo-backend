namespace UTTTGO.Api.Modules.Media.Contracts;

public class PresignedUrlRequest
{
    [System.Text.Json.Serialization.JsonRequired]
    [System.Text.Json.Serialization.JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonRequired]
    [System.Text.Json.Serialization.JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonRequired]
    [System.Text.Json.Serialization.JsonPropertyName("size")]
    public long Size { get; set; }
}

public class PresignedUrlResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("assetId")]
    public string AssetId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("uploadUrl")]
    public string UploadUrl { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("objectKey")]
    public string ObjectKey { get; set; } = string.Empty;
}

public class ConfirmRequest
{
    [System.Text.Json.Serialization.JsonRequired]
    [System.Text.Json.Serialization.JsonPropertyName("assetId")]
    public string AssetId { get; set; } = string.Empty;
}

public class ConfirmResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("publicUrl")]
    public string PublicUrl { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("assetId")]
    public string AssetId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}