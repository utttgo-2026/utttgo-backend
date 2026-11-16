using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

// A reservation holds stock for an order. The reservation id makes the release
// idempotent: a repeated release finds no reservation and changes nothing.
public sealed class StockReservation
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }
}
