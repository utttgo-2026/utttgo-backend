using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

[BsonIgnoreExtraElements]
public sealed class ProductDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("sellerId")]
    public string SellerId { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("normalizedName")]
    public string NormalizedName { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("ingredients")]
    public IReadOnlyList<string> Ingredients { get; set; } = [];

    [BsonElement("categoryId")]
    public string CategoryId { get; set; } = string.Empty;

    [BsonElement("mediaAssetId")]
    public string? MediaAssetId { get; set; }

    // Money is stored as Decimal128; the API never converts it to a floating point number.
    [BsonElement("price")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Price { get; set; }

    [BsonElement("stock")]
    public int Stock { get; set; }

    [BsonElement("stockReservations")]
    public IReadOnlyList<StockReservation> StockReservations { get; set; } = [];

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    // Normalized name, description and ingredients joined, used by the bounded text search.
    [BsonElement("searchText")]
    public string SearchText { get; set; } = string.Empty;

    [BsonElement("version")]
    public long Version { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }
}
