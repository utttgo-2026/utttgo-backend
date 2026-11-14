using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Catalog.Contracts;

// Availability is a publication state, not a stock state: pausing a product never turns it into
// a sold out product and it never changes the stored stock.
public sealed class UpdateProductAvailabilityRequest
{
    public string? Availability { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}
