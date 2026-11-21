using UTTTGO.Api.Modules.Catalog.Contracts;

namespace UTTTGO.Api.Modules.Catalog.Application;

// The Catalog module owns the products collection. Only this module reads or writes it.
public interface IProductRepository
{
    Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken cancellationToken);
    Task<ProductRecord?> FindByIdAsync(string productId, CancellationToken cancellationToken);
    Task<ProductRecord> CreateAsync(NewProduct product, CancellationToken cancellationToken);
    Task<ProductUpdateResult> UpdateOwnAsync(string sellerId, string productId, long expectedVersion,
        ProductChanges changes, CancellationToken cancellationToken);
    Task<ProductUpdateResult> UpdateAvailabilityAsync(string sellerId, string productId, long expectedVersion,
        string status, CancellationToken cancellationToken);
}

// The Catalog module owns the categories collection as well.
public interface ICategoryRepository
{
    Task<CategoryRecord?> FindByIdAsync(string categoryId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryRecord>> ListActiveAsync(CancellationToken cancellationToken);
    Task<CategoryPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public sealed record ProductRecord(string ProductId, string SellerId, string Name, string Description,
    IReadOnlyList<string> Ingredients, string CategoryId, decimal Price, int Stock, string Status,
    long Version, DateTime CreatedAt, DateTime UpdatedAt, string? ImageUrl = null);

public sealed record ProductPage(IReadOnlyList<ProductRecord> Items, long Total);

public sealed record NewProduct(string SellerId, string Name, string Description,
    IReadOnlyList<string> Ingredients, string CategoryId, decimal Price, int Stock,
    string? ImageUrl = null);

public sealed record ProductChanges(bool HasName, string? Name, bool HasDescription, string? Description,
    bool HasIngredients, IReadOnlyList<string>? Ingredients, bool HasPrice, decimal? Price, bool HasStock,
    int? Stock, bool HasImageUrl = false, string? ImageUrl = null);

public enum ProductUpdateOutcome { Updated, Missing, Conflict }

public sealed record ProductUpdateResult(ProductUpdateOutcome Outcome, ProductRecord? Product = null);

public sealed record CategoryRecord(string CategoryId, string Name, bool IsActive, int SortOrder);

public sealed record CategoryPage(IReadOnlyList<CategoryRecord> Items, long Total);

// Distributed rate limiting: public reads fail closed and seller writes are partitioned by owner.
public interface ICatalogRateLimitGate
{
    Task<bool> IsAllowedForPublicReadAsync(CancellationToken cancellationToken);
    Task<bool> IsAllowedForWriteAsync(string sellerId, CancellationToken cancellationToken);
}

// Reviews owns the ratings. No rating is inferred from product records or from seller data.
public interface IProductRatingsReader
{
    Task<ProductRatingsSnapshot?> GetForProductAsync(string productId, CancellationToken cancellationToken);
}

public sealed record ProductRatingsSnapshot(decimal? Average, long Count);
