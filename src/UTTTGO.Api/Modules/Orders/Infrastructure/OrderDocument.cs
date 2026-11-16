using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UTTTGO.Api.Modules.Orders.Infrastructure;

[BsonIgnoreExtraElements]
public sealed class OrderDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("buyerUserId")]
    public string BuyerUserId { get; set; } = string.Empty;

    [BsonElement("sellerId")]
    public string SellerId { get; set; } = string.Empty;

    [BsonElement("productId")]
    public string ProductId { get; set; } = string.Empty;

    [BsonElement("productName")]
    public string ProductName { get; set; } = string.Empty;

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("unitPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal UnitPrice { get; set; }

    [BsonElement("total")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Total { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = "MXN";

    [BsonElement("paymentMethod")]
    public string PaymentMethod { get; set; } = "Cash";

    [BsonElement("status")]
    public string Status { get; set; } = "Pending";

    [BsonElement("reservationId")]
    public string ReservationId { get; set; } = string.Empty;

    [BsonElement("pickupPoint")]
    public string PickupPoint { get; set; } = string.Empty;

    [BsonElement("deliveredAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? DeliveredAt { get; set; }

    [BsonElement("completedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? CompletedAt { get; set; }

    [BsonElement("version")]
    public long Version { get; set; }

    [BsonElement("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }
}
