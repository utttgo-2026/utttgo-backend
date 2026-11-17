using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Sellers.Contracts;

// Public activation contract: storeName and description describe the business and
// phone is the contact the buyer uses to coordinate the on-campus pickup.
public sealed class SellerActivationRequest
{
    public string? StoreName { get; init; }
    public string? Description { get; init; }
    public string? Phone { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}
