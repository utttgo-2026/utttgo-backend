using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Orders.Contracts;

public sealed class DeliveryConfirmationRequest
{
    public string? Token { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}
