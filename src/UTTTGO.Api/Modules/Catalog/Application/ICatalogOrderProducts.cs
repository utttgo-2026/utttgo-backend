namespace UTTTGO.Api.Modules.Catalog.Application;

// Published by Catalog so that Orders never reads the products or categories
// collections: only the owning module knows how a product is stored.
public interface ICatalogOrderProducts
{
    Task<CatalogOrderProduct?> FindForOrderAsync(string productId, CancellationToken cancellationToken);
    Task<CatalogStockOutcome> ReserveStockAsync(string productId, int quantity, string reservationId,
        CancellationToken cancellationToken);
    Task<bool> ReleaseStockAsync(string productId, int quantity, string reservationId,
        CancellationToken cancellationToken);
}

public sealed record CatalogOrderProduct(string ProductId, string SellerId, string Name, decimal UnitPrice,
    int Stock, bool IsPublished);

public enum CatalogStockOutcome { Reserved, InsufficientStock, NotFound, NotAvailable }
