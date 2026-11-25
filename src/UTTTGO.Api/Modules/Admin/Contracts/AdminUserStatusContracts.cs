using System.Text.Json;
using System.Text.Json.Serialization;

namespace UTTTGO.Api.Modules.Admin.Contracts;

public sealed class AdminUserStatusRequest
{
    [JsonPropertyName("accountStatus")]
    public string? AccountStatus { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed record AdminUserStatusResponse(string UserId, string AccountStatus);
