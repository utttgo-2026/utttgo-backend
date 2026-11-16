using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Catalog.Infrastructure;

public sealed class MongoOrderProductRepository(MongoStore store) : ICatalogOrderProducts
{
    private IMongoCollection<ProductDocument> Products =>
        store.Database.GetCollection<ProductDocument>("products");

    private IMongoCollection<CategoryDocument> Categories =>
        store.Database.GetCollection<CategoryDocument>("categories");

    public async Task<CatalogOrderProduct?> FindForOrderAsync(string productId,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(productId, out var id)) return null;
        var product = await Products.Find(item => item.Id == id).FirstOrDefaultAsync(cancellationToken);
        if (product is null) return null;

        var category = await Categories.Find(item => item.Id.ToString() == product.CategoryId)
            .FirstOrDefaultAsync(cancellationToken);
        var published = product.Status == ProductAvailability.Active && category is { IsActive: true };

        return new CatalogOrderProduct(product.Id.ToString(), product.SellerId, product.Name,
            product.Price, product.Stock, published);
    }

    public async Task<CatalogStockOutcome> ReserveStockAsync(string productId, int quantity,
        string reservationId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(productId, out var id)) return CatalogStockOutcome.NotFound;

        // The stock check and the decrement are one atomic operation: two buyers can
        // never reserve the same unit.
        var filter = Builders<ProductDocument>.Filter.Eq(item => item.Id, id) &
            Builders<ProductDocument>.Filter.Eq(item => item.Status, ProductAvailability.Active) &
            Builders<ProductDocument>.Filter.Gte(item => item.Stock, quantity);
        var update = Builders<ProductDocument>.Update
            .Inc(item => item.Stock, -quantity)
            .Inc(item => item.Version, 1)
            .Push(item => item.StockReservations, new StockReservation
            {
                Id = reservationId,
                Quantity = quantity,
                CreatedAt = DateTime.UtcNow
            });
        var reserved = await Products.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<ProductDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (reserved is not null) return CatalogStockOutcome.Reserved;

        var current = await Products.Find(item => item.Id == id).FirstOrDefaultAsync(cancellationToken);
        if (current is null) return CatalogStockOutcome.NotFound;
        if (current.Status != ProductAvailability.Active) return CatalogStockOutcome.NotAvailable;
        return CatalogStockOutcome.InsufficientStock;
    }

    public async Task<bool> ReleaseStockAsync(string productId, int quantity, string reservationId,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(productId, out var id)) return false;

        // Only a reservation that still exists releases the stock: repeating the release
        // is a no-op and never gives back the same units twice.
        var filter = Builders<ProductDocument>.Filter.Eq(item => item.Id, id) &
            Builders<ProductDocument>.Filter.Eq("stockReservations.id", reservationId);
        var update = Builders<ProductDocument>.Update
            .Inc(item => item.Stock, quantity)
            .Inc(item => item.Version, 1)
            .PullFilter(item => item.StockReservations, reservation =>
                reservation.Id == reservationId);
        var released = await Products.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<ProductDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return released is not null;
    }
}
