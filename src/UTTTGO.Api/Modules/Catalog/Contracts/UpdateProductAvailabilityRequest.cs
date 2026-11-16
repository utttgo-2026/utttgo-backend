using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Catalog.Contracts;

// Availability is a publication state, not a stock state: pausing a product never turns it into
// a sold out product and it never changes the stored stock. The JSON property is "status" with
// the values "Active" and "Paused", the same vocabulary the product document uses.
public sealed class UpdateProductAvailabilityRequest
{
    public string? Status { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}
