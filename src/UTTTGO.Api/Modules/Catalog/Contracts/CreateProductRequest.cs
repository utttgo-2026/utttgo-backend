using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Catalog.Contracts;

// A seller publishes its own product. The client never sends sellerId, status, version or
// timestamps: the backend derives them from the authenticated session.
// imageUrl is an optional Media reference: the assetId or canonical publicUrl returned by
// confirmation. The backend stores only the asset identity/key and rejects it with 400
// VALIDATION_ERROR unless it is Ready/Approved, belongs to the caller and uses staging/.
public sealed class CreateProductRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string>? Ingredients { get; init; }
    public string? CategoryId { get; init; }
    public decimal? Price { get; init; }
    public int? Stock { get; init; }
    public string? ImageUrl { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}
