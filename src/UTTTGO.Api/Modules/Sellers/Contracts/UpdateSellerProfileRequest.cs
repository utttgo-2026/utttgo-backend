using System.Text.Json;

namespace UTTTGO.Api.Modules.Sellers.Contracts;

// Explicit presence flags preserve PATCH omission; null is rejected until deletion rules are approved.
public sealed record UpdateSellerProfileRequest(bool HasBusinessName, string? BusinessName,
    bool HasDescription, string? Description, bool HasPickupPoint, string? PickupPoint)
{
    public static bool TryParse(JsonElement json, out UpdateSellerProfileRequest? request)
    {
        request = null;
        if (json.ValueKind != JsonValueKind.Object) return false;

        string? businessName = null;
        string? description = null;
        string? pickupPoint = null;
        var hasBusinessName = false;
        var hasDescription = false;
        var hasPickupPoint = false;
        foreach (var field in json.EnumerateObject())
        {
            if (field.Name.Equals("businessName", StringComparison.Ordinal))
            {
                if (hasBusinessName || field.Value.ValueKind != JsonValueKind.String) return false;
                hasBusinessName = true;
                businessName = field.Value.GetString();
            }
            else if (field.Name.Equals("description", StringComparison.Ordinal))
            {
                if (hasDescription || field.Value.ValueKind != JsonValueKind.String) return false;
                hasDescription = true;
                description = field.Value.GetString();
            }
            else if (field.Name.Equals("pickupPoint", StringComparison.Ordinal))
            {
                if (hasPickupPoint || field.Value.ValueKind != JsonValueKind.String) return false;
                hasPickupPoint = true;
                pickupPoint = field.Value.GetString();
            }
            else return false; // Strict whitelist: roles, sellerId, status and version are never accepted.
        }

        if (!hasBusinessName && !hasDescription && !hasPickupPoint) return false;
        request = new(hasBusinessName, businessName, hasDescription, description, hasPickupPoint, pickupPoint);
        return true;
    }
}
