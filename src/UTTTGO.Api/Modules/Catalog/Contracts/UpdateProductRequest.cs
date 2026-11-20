using System.Text.Json;

namespace UTTTGO.Api.Modules.Catalog.Contracts;

// Explicit presence flags distinguish image removal (null) from omission.
// The whitelist never accepts sellerId, status, version or categoryId.
public sealed record UpdateProductRequest(bool HasName, string? Name, bool HasDescription,
    string? Description, bool HasIngredients, IReadOnlyList<string>? Ingredients, bool HasPrice,
    decimal? Price, bool HasStock, int? Stock, bool HasMediaAssetId = false, string? MediaAssetId = null)
{
    public static bool TryParse(JsonElement json, out UpdateProductRequest? request)
    {
        request = null;
        if (json.ValueKind != JsonValueKind.Object) return false;

        string? name = null;
        string? description = null;
        IReadOnlyList<string>? ingredients = null;
        decimal? price = null;
        int? stock = null;
        var hasName = false;
        var hasDescription = false;
        var hasIngredients = false;
        var hasPrice = false;
        var hasStock = false;
        var hasMediaAssetId = false;
        string? mediaAssetId = null;

        foreach (var field in json.EnumerateObject())
        {
            if (field.Name.Equals("name", StringComparison.Ordinal))
            {
                if (hasName || field.Value.ValueKind != JsonValueKind.String) return false;
                hasName = true;
                name = field.Value.GetString();
            }
            else if (field.Name.Equals("description", StringComparison.Ordinal))
            {
                if (hasDescription || field.Value.ValueKind != JsonValueKind.String) return false;
                hasDescription = true;
                description = field.Value.GetString();
            }
            else if (field.Name.Equals("ingredients", StringComparison.Ordinal))
            {
                if (hasIngredients || field.Value.ValueKind != JsonValueKind.Array) return false;
                var parsed = new List<string>();
                foreach (var item in field.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) return false;
                    parsed.Add(item.GetString()!);
                }

                hasIngredients = true;
                ingredients = parsed;
            }
            else if (field.Name.Equals("price", StringComparison.Ordinal))
            {
                if (hasPrice || !field.Value.TryGetDecimal(out var parsedPrice)) return false;
                hasPrice = true;
                price = parsedPrice;
            }
            else if (field.Name.Equals("stock", StringComparison.Ordinal))
            {
                if (hasStock || !field.Value.TryGetInt32(out var parsedStock)) return false;
                hasStock = true;
                stock = parsedStock;
            }
            else if (field.NameEquals("mediaAssetId"))
            {
                if (hasMediaAssetId || field.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                    return false;
                hasMediaAssetId = true;
                mediaAssetId = field.Value.ValueKind == JsonValueKind.Null ? null : field.Value.GetString();
            }
            else return false; // Strict whitelist: ownership and privileged fields are never accepted.
        }

        if (!hasName && !hasDescription && !hasIngredients && !hasPrice && !hasStock &&
            !hasMediaAssetId) return false;
        request = new(hasName, name, hasDescription, description, hasIngredients, ingredients, hasPrice,
            price, hasStock, stock, hasMediaAssetId, mediaAssetId);
        return true;
    }
}
