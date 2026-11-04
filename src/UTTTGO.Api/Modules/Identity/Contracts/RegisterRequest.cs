using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Identity.Contracts;

public sealed class RegisterRequest
{
    public string? Email { get; init; }
    public string? Password { get; init; }
    public string? DisplayName { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}
