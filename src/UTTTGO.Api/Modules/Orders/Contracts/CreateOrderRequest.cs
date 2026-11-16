using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Orders.Contracts;

// Direct purchase of one product (BR-001/BR-002). The client never sends the price,
// the total, the seller, the status, the payment method or the currency (BR-009).
public sealed class CreateOrderRequest
{
    public string? ProductId { get; init; }

    public int? Quantity { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}
