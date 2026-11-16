using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Orders.Infrastructure;

public sealed class MongoOrderRepository(MongoStore store) : IOrderRepository
{
    private IMongoCollection<OrderDocument> Orders =>
        store.Database.GetCollection<OrderDocument>("orders");

    public async Task<OrderRecord> CreateAsync(OrderRecord order, CancellationToken cancellationToken)
    {
        var document = new OrderDocument
        {
            Id = ObjectId.GenerateNewId(),
            BuyerUserId = order.BuyerUserId,
            SellerId = order.SellerId,
            ProductId = order.ProductId,
            ProductName = order.ProductName,
            Quantity = order.Quantity,
            UnitPrice = order.UnitPrice,
            Total = order.Total,
            Currency = order.Currency,
            PaymentMethod = order.PaymentMethod,
            Status = order.Status,
            ReservationId = order.ReservationId,
            PickupPoint = order.PickupPoint,
            DeliveredAt = order.DeliveredAt,
            CompletedAt = order.CompletedAt,
            Version = order.Version,
            IdempotencyKey = order.IdempotencyKey,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };

        try
        {
            await Orders.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // The partial unique index on (buyerUserId, idempotencyKey) rejected a replay.
            throw new DuplicateOrderException();
        }

        return ToRecord(document);
    }

    public async Task<OrderRecord?> FindByIdAsync(string orderId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(orderId, out var id)) return null;
        var document = await Orders.Find(item => item.Id == id).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToRecord(document);
    }

    public async Task<OrderRecord?> FindByIdempotencyKeyAsync(string buyerUserId, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var filter = Builders<OrderDocument>.Filter.Eq(item => item.BuyerUserId, buyerUserId) &
            Builders<OrderDocument>.Filter.Eq(item => item.IdempotencyKey, idempotencyKey);
        var document = await Orders.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToRecord(document);
    }

    public async Task<OrderPage> ListForBuyerAsync(string buyerUserId, string? status, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var filter = Builders<OrderDocument>.Filter.Eq(item => item.BuyerUserId, buyerUserId);
        if (!string.IsNullOrWhiteSpace(status))
            filter &= Builders<OrderDocument>.Filter.Eq(item => item.Status, status);

        var total = await Orders.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var skip = (int)((long)(page - 1) * pageSize);
        var documents = await Orders.Find(filter)
            .Sort(Builders<OrderDocument>.Sort.Descending(item => item.CreatedAt).Ascending(item => item.Id))
            .Skip(skip).Limit(pageSize).ToListAsync(cancellationToken);
        return new(documents.Select(ToRecord).ToList(), total);
    }

    private static OrderRecord ToRecord(OrderDocument order) =>
        new(order.Id.ToString(), order.BuyerUserId, order.SellerId, order.ProductId, order.ProductName,
            order.Quantity, order.UnitPrice, order.Total, order.Currency, order.PaymentMethod, order.Status,
            order.ReservationId, order.PickupPoint, order.DeliveredAt, order.CompletedAt, order.Version,
            order.IdempotencyKey, order.CreatedAt, order.UpdatedAt);
}
