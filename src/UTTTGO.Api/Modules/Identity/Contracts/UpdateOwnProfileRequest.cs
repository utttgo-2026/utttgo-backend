using System.Text.Json;

namespace UTTTGO.Api.Modules.Identity.Contracts;

// Explicit presence flags preserve PATCH omission; null is rejected until deletion rules are approved.
public sealed record UpdateOwnProfileRequest(bool HasDisplayName, string? DisplayName,
    bool HasPhone, string? Phone)
{
    public static bool TryParse(JsonElement json, out UpdateOwnProfileRequest? request)
    {
        request = null;
        if (json.ValueKind != JsonValueKind.Object) return false;

        string? displayName = null;
        string? phone = null;
        var hasDisplayName = false;
        var hasPhone = false;
        foreach (var field in json.EnumerateObject())
        {
            if (field.Name.Equals("displayName", StringComparison.Ordinal))
            {
                if (hasDisplayName || field.Value.ValueKind != JsonValueKind.String) return false;
                hasDisplayName = true;
                displayName = field.Value.GetString();
            }
            else if (field.Name.Equals("phone", StringComparison.Ordinal))
            {
                if (hasPhone || field.Value.ValueKind != JsonValueKind.String) return false;
                hasPhone = true;
                phone = field.Value.GetString();
            }
            else return false; // Strict whitelist: includes avatarUrl/mediaAssetId until Media write contract approved.
        }

        if (!hasDisplayName && !hasPhone) return false;
        request = new(hasDisplayName, displayName, hasPhone, phone);
        return true;
    }
}
