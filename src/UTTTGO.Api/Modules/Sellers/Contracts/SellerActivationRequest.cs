using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Sellers.Contracts;

public sealed class SellerActivationRequest
{
    public string? BusinessName { get; init; }
    public string? Description { get; init; }
    public string? PickupPoint { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}
