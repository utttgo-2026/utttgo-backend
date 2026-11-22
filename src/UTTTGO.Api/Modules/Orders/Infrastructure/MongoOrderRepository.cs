using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Orders.Infrastructure;

public sealed class MongoOrderRepository(MongoStore store) : IOrderRepository, IActiveOrderReader
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
            Notes = order.Notes,
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

    public Task<bool> HasActiveOrdersAsync(string buyerUserId, string? sellerId,
        CancellationToken cancellationToken)
    {
        var filter = Builders<OrderDocument>.Filter.In(item => item.Status,
            new[] { "Pending", "Accepted", "InPreparation", "Ready" });
        filter &= Builders<OrderDocument>.Filter.Or(
            Builders<OrderDocument>.Filter.Eq(item => item.BuyerUserId, buyerUserId),
            string.IsNullOrWhiteSpace(sellerId)
                ? Builders<OrderDocument>.Filter.Exists(item => item.SellerId, false)
                : Builders<OrderDocument>.Filter.Eq(item => item.SellerId, sellerId));
        return Orders.Find(filter).Limit(1).AnyAsync(cancellationToken);
    }

    public async Task<OrderRecord?> FindByIdempotencyKeyAsync(string buyerUserId, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var filter = Builders<OrderDocument>.Filter.Eq(item => item.BuyerUserId, buyerUserId) &
            Builders<OrderDocument>.Filter.Eq(item => item.IdempotencyKey, idempotencyKey);
        var document = await Orders.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToRecord(document);
    }

    public Task<OrderPage> ListForBuyerAsync(string buyerUserId, string? status, int page, int pageSize,
        CancellationToken cancellationToken) =>
        ListByParticipantAsync(Builders<OrderDocument>.Filter.Eq(item => item.BuyerUserId, buyerUserId),
            status, page, pageSize, cancellationToken);

    public Task<OrderPage> ListForSellerAsync(string sellerId, string? status, int page, int pageSize,
        CancellationToken cancellationToken) =>
        ListByParticipantAsync(Builders<OrderDocument>.Filter.Eq(item => item.SellerId, sellerId),
            status, page, pageSize, cancellationToken);

    private async Task<OrderPage> ListByParticipantAsync(FilterDefinition<OrderDocument> participant,
        string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var filter = participant;
        if (!string.IsNullOrWhiteSpace(status))
            filter &= Builders<OrderDocument>.Filter.Eq(item => item.Status, status);

        var total = await Orders.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var skip = (int)((long)(page - 1) * pageSize);
        var documents = await Orders.Find(filter)
            .Sort(Builders<OrderDocument>.Sort.Descending(item => item.CreatedAt).Ascending(item => item.Id))
            .Skip(skip).Limit(pageSize).ToListAsync(cancellationToken);
        return new(documents.Select(ToRecord).ToList(), total);
    }

    public async Task<OrderRecord?> ApplyAsync(string orderId, string expectedStatus, long expectedVersion,
        string? expectedDeliveryTokenHash, OrderMutation mutation, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(orderId, out var id)) return null;

        var filter = Builders<OrderDocument>.Filter.Eq(item => item.Id, id) &
            Builders<OrderDocument>.Filter.Eq(item => item.Status, expectedStatus) &
            Builders<OrderDocument>.Filter.Eq(item => item.Version, expectedVersion);
        if (expectedDeliveryTokenHash is not null)
            filter &= Builders<OrderDocument>.Filter.Eq(item => item.DeliveryTokenHash,
                expectedDeliveryTokenHash);

        var update = Builders<OrderDocument>.Update
            .Set(item => item.UpdatedAt, DateTime.UtcNow)
            .Inc(item => item.Version, 1);
        if (mutation.NewStatus is not null)
            update = update.Set(item => item.Status, mutation.NewStatus);
        if (mutation.DeliveredAt is not null)
            update = update.Set(item => item.DeliveredAt, mutation.DeliveredAt);
        if (mutation.CompletedAt is not null)
            update = update.Set(item => item.CompletedAt, mutation.CompletedAt);
        if (mutation.DeliveryTokenHash is not null)
            update = update.Set(item => item.DeliveryTokenHash, mutation.DeliveryTokenHash);
        if (mutation.DeliveryTokenExpiresAt is not null)
            update = update.Set(item => item.DeliveryTokenExpiresAt, mutation.DeliveryTokenExpiresAt);
        if (mutation.DeliveryTokenUsedAt is not null)
            update = update.Set(item => item.DeliveryTokenUsedAt, mutation.DeliveryTokenUsedAt);
        if (mutation.ClearDeliveryTokenUsedAt)
            update = update.Unset(item => item.DeliveryTokenUsedAt);

        var updated = await Orders.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<OrderDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return updated is null ? null : ToRecord(updated);
    }

    private static OrderRecord ToRecord(OrderDocument order) =>
        new(order.Id.ToString(), order.BuyerUserId, order.SellerId, order.ProductId, order.ProductName,
            order.Quantity, order.UnitPrice, order.Total, order.Currency, order.PaymentMethod, order.Status,
            order.ReservationId, order.PickupPoint, order.DeliveredAt, order.CompletedAt, order.Version,
            order.IdempotencyKey, order.CreatedAt, order.UpdatedAt, order.Notes, order.DeliveryTokenHash,
            order.DeliveryTokenExpiresAt, order.DeliveryTokenUsedAt);
}
