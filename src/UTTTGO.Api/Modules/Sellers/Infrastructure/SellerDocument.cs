using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UTTTGO.Api.Modules.Sellers.Infrastructure;

[BsonIgnoreExtraElements]
public sealed class SellerDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("ownerUserId")]
    public string OwnerUserId { get; set; } = string.Empty;

    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("pickupPoint")]
    public string PickupPoint { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("version")]
    public long Version { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }
}
