using System.Text.Json;

namespace UTTTGO.Api.Modules.Sellers.Contracts;

// Explicit presence flags preserve PATCH omission; null is rejected until deletion rules are approved.
public sealed record UpdateSellerProfileRequest(bool HasStoreName, string? StoreName,
    bool HasDescription, string? Description, bool HasPhone, string? Phone)
{
    public static bool TryParse(JsonElement json, out UpdateSellerProfileRequest? request)
    {
        request = null;
        if (json.ValueKind != JsonValueKind.Object) return false;

        string? storeName = null;
        string? description = null;
        string? phone = null;
        var hasStoreName = false;
        var hasDescription = false;
        var hasPhone = false;
        foreach (var field in json.EnumerateObject())
        {
            if (field.Name.Equals("storeName", StringComparison.Ordinal))
            {
                if (hasStoreName || field.Value.ValueKind != JsonValueKind.String) return false;
                hasStoreName = true;
                storeName = field.Value.GetString();
            }
            else if (field.Name.Equals("description", StringComparison.Ordinal))
            {
                if (hasDescription || field.Value.ValueKind != JsonValueKind.String) return false;
                hasDescription = true;
                description = field.Value.GetString();
            }
            else if (field.Name.Equals("phone", StringComparison.Ordinal))
            {
                if (hasPhone || field.Value.ValueKind != JsonValueKind.String) return false;
                hasPhone = true;
                phone = field.Value.GetString();
            }
            else return false; // Strict whitelist: roles, sellerId, status and version are never accepted.
        }

        if (!hasStoreName && !hasDescription && !hasPhone) return false;
        request = new(hasStoreName, storeName, hasDescription, description, hasPhone, phone);
        return true;
    }
}
