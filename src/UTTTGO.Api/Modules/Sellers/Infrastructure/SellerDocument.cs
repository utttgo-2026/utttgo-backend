using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UTTTGO.Api.Modules.Sellers.Infrastructure;

[BsonIgnoreExtraElements]
public sealed class SellerDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;
}
