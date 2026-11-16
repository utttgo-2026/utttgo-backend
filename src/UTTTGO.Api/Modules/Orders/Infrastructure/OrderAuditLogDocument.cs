using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UTTTGO.Api.Modules.Orders.Infrastructure;

[BsonIgnoreExtraElements]
public sealed class OrderAuditLogDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [BsonElement("actorUserId")]
    public string ActorUserId { get; set; } = string.Empty;

    [BsonElement("actorRole")]
    public string ActorRole { get; set; } = string.Empty;

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("fromStatus")]
    public string? FromStatus { get; set; }

    [BsonElement("toStatus")]
    public string? ToStatus { get; set; }

    [BsonElement("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    [BsonElement("occurredAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime OccurredAt { get; set; }
}
