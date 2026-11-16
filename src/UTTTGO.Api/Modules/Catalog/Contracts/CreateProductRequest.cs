using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Catalog.Contracts;

// A seller publishes its own product. The client never sends sellerId, status, version,
// timestamps or media references: the backend derives them from the authenticated session.
// imageUrl is accepted as an optional reference so the mobile contract stays stable, but it is
// never stored: while Media (S3 presigned + Azure AI Content Safety, phase 05) does not exist,
// an image can neither be uploaded nor moderated, so the backend answers 422 IMAGE_NOT_AVAILABLE
// and the product is published without a photo (BR-007, DEC-019.f, RF-V11).
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
