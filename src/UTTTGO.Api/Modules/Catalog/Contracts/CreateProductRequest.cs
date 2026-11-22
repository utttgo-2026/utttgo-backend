using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Catalog.Contracts;

// A seller publishes its own product. The client never sends sellerId, status, version,
// timestamps. mediaAssetId is an optional approved Media reference, never a public URL.
// The legacy imageUrl input is rejected; only backend-presigned reads are returned.
public sealed class CreateProductRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string>? Ingredients { get; init; }
    public string? CategoryId { get; init; }
    public decimal? Price { get; init; }
    public int? Stock { get; init; }
    public string? ImageUrl { get; init; }
    public string? MediaAssetId { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}
