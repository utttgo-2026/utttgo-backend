using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UTTTGO.Api.Modules.Reviews.Infrastructure;

[BsonIgnoreExtraElements]
public sealed class ReviewDocument
{
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("orderId")] public string OrderId { get; set; } = string.Empty;
    [BsonElement("buyerUserId")] public string BuyerUserId { get; set; } = string.Empty;
    [BsonElement("sellerId")] public string SellerId { get; set; } = string.Empty;
    [BsonElement("productId")] public string ProductId { get; set; } = string.Empty;
    [BsonElement("rating")] public int Rating { get; set; }
    [BsonElement("comment")] public string? Comment { get; set; }
    [BsonElement("createdAt"), BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }
}
