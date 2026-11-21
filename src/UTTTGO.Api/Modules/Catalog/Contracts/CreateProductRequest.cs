using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Catalog.Contracts;

// A seller publishes its own product. The client never sends sellerId, status, version or
// timestamps: the backend derives them from the authenticated session.
// imageUrl is an optional reference to a Media asset that this seller already confirmed. The
// backend resolves it through the Media read contract and rejects it with 400 VALIDATION_ERROR
// unless the asset exists, is Ready, belongs to the caller and is served from the configured
// S3 host (BR-007, DEC-019.f, RF-V11).
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
