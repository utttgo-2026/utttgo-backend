using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Catalog.Contracts;

// A seller publishes its own product. The client never sends sellerId, status, version,
// timestamps or media references: the backend derives them from the authenticated session.
public sealed class CreateProductRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string>? Ingredients { get; init; }
    public string? CategoryId { get; init; }
    public decimal? Price { get; init; }
    public int? Stock { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}
