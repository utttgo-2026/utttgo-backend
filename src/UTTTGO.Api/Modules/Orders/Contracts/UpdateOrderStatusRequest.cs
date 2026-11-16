using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Orders.Contracts;

public sealed class UpdateOrderStatusRequest
{
    public string? Status { get; init; }

    public long? Version { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}
