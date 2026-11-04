using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Identity.Contracts;

public sealed class LoginRequest
{
    public string? Email { get; init; }
    public string? Password { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}
