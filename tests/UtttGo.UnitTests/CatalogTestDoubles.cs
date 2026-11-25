using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Sellers.Application;

namespace UtttGo.UnitTests;

internal sealed class CatalogProductRepository : IProductRepository
{
    public Dictionary<string, ProductRecord> Products { get; } = new(StringComparer.Ordinal);
    public int ListCalls { get; private set; }
    public int FindCalls { get; private set; }
    public int CreateCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public string? LastSellerId { get; private set; }
    public bool Fail { get; set; }
    public bool ForceConflict { get; set; }
    public bool ForceTampered { get; set; }
    public ProductListQuery? LastQuery { get; private set; }

    public Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        ListCalls++;
        LastQuery = query;
        var items = Products.Values
            .Where(product => product.Status == ProductAvailability.Active)
            .Where(product => query.AllowedCategoryIds is null ||
                query.AllowedCategoryIds.Contains(product.CategoryId))
            .OrderByDescending(product => product.CreatedAt)
            .ToList();
        return Task.FromResult(new ProductPage(items, items.Count));
    }

    public Task<ProductPage> ListForSellerAsync(string sellerId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        var items = Products.Values.Where(product => product.SellerId == sellerId &&
                product.Status != ProductAvailability.Deleted)
            .OrderByDescending(product => product.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new ProductPage(items, items.Count));
    }

    public Task<ProductRecord?> FindByIdAsync(string productId, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        FindCalls++;
        Products.TryGetValue(productId, out var product);
        return Task.FromResult(product);
    }

    public Task<ProductRecord> CreateAsync(NewProduct product, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        CreateCalls++;
        LastSellerId = product.SellerId;
        var record = new ProductRecord(CatalogFixtures.ProductId, product.SellerId, product.Name,
            product.Description, product.Ingredients, product.CategoryId, product.Price, product.Stock,
            ProductAvailability.Active, 0, DateTime.UtcNow, DateTime.UtcNow, product.MediaAssetId);
        if (ForceTampered) record = record with { SellerId = "other-seller" };
        Products[record.ProductId] = record;
        return Task.FromResult(record);
    }

    public Task<ProductUpdateResult> UpdateOwnAsync(string sellerId, string productId, long expectedVersion,
        ProductChanges changes, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        UpdateCalls++;
        LastSellerId = sellerId;
        if (ForceConflict) return Task.FromResult(new ProductUpdateResult(ProductUpdateOutcome.Conflict));
        if (!Products.TryGetValue(productId, out var product) || product.SellerId != sellerId)
            return Task.FromResult(new ProductUpdateResult(ProductUpdateOutcome.Missing));
        if (product.Version != expectedVersion)
            return Task.FromResult(new ProductUpdateResult(ProductUpdateOutcome.Conflict));

        var updated = product with
        {
            Name = changes.HasName ? changes.Name! : product.Name,
            Description = changes.HasDescription ? changes.Description! : product.Description,
            Ingredients = changes.HasIngredients ? changes.Ingredients! : product.Ingredients,
            Price = changes.HasPrice ? changes.Price!.Value : product.Price,
            Stock = changes.HasStock ? changes.Stock!.Value : product.Stock,
            MediaAssetId = changes.HasMediaAssetId ? changes.MediaAssetId : product.MediaAssetId,
            Version = product.Version + 1,
            UpdatedAt = DateTime.UtcNow
        };
        if (ForceTampered) updated = updated with { SellerId = "other-seller" };
        Products[productId] = updated;
        return Task.FromResult(new ProductUpdateResult(ProductUpdateOutcome.Updated, updated));
    }

    public Task<ProductUpdateResult> UpdateAvailabilityAsync(string sellerId, string productId,
        long expectedVersion, string status, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        UpdateCalls++;
        LastSellerId = sellerId;
        if (ForceConflict) return Task.FromResult(new ProductUpdateResult(ProductUpdateOutcome.Conflict));
        if (!Products.TryGetValue(productId, out var product) || product.SellerId != sellerId)
            return Task.FromResult(new ProductUpdateResult(ProductUpdateOutcome.Missing));
        if (product.Version != expectedVersion)
            return Task.FromResult(new ProductUpdateResult(ProductUpdateOutcome.Conflict));

        var updated = product with
        {
            Status = status,
            Version = product.Version + 1,
            UpdatedAt = DateTime.UtcNow
        };
        if (ForceTampered) updated = updated with { Stock = product.Stock + 1 };
        Products[productId] = updated;
        return Task.FromResult(new ProductUpdateResult(ProductUpdateOutcome.Updated, updated));
    }
}

internal sealed class CatalogCategoryRepository : ICategoryRepository
{
    public Dictionary<string, CategoryRecord> Categories { get; } = new(StringComparer.Ordinal);
    public bool Fail { get; set; }

    public Task<CategoryRecord?> FindByIdAsync(string categoryId, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        Categories.TryGetValue(categoryId, out var category);
        return Task.FromResult(category);
    }

    public Task<IReadOnlyList<CategoryRecord>> ListActiveAsync(CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        IReadOnlyList<CategoryRecord> categories = Categories.Values
            .Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder)
            .ToList();
        return Task.FromResult(categories);
    }

    public Task<CategoryPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        var categories = Categories.Values.Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder).ToList();
        return Task.FromResult(new CategoryPage(categories, categories.Count));
    }
}

internal sealed class CatalogRateLimitGate : ICatalogRateLimitGate
{
    public int Calls { get; private set; }
    public bool Allowed { get; set; } = true;
    public bool Fail { get; set; }

    public Task<bool> IsAllowedForPublicReadAsync(CancellationToken cancellationToken)
    {
        Calls++;
        if (Fail) throw new InvalidOperationException("internal-secret");
        return Task.FromResult(Allowed);
    }

    public Task<bool> IsAllowedForWriteAsync(string sellerId, CancellationToken cancellationToken)
    {
        Calls++;
        if (Fail) throw new InvalidOperationException("internal-secret");
        return Task.FromResult(Allowed);
    }
}

internal sealed class CatalogCacheDouble : ICatalogCache
{
    public ProductListResponse? ProductResponse { get; set; }
    public CategoryListResponse? CategoryResponse { get; set; }
    public int ProductGets { get; private set; }
    public int ProductSets { get; private set; }
    public int CategoryGets { get; private set; }
    public int CategorySets { get; private set; }
    public int Invalidations { get; private set; }

    public Task<ProductListResponse?> GetProductsAsync(ProductListQuery query, CancellationToken cancellationToken)
    {
        ProductGets++;
        return Task.FromResult(ProductResponse);
    }

    public Task SetProductsAsync(ProductListQuery query, ProductListResponse response,
        CancellationToken cancellationToken)
    {
        ProductSets++;
        ProductResponse = response;
        return Task.CompletedTask;
    }

    public Task<CategoryListResponse?> GetCategoriesAsync(int page, int pageSize,
        CancellationToken cancellationToken)
    {
        CategoryGets++;
        return Task.FromResult(CategoryResponse);
    }

    public Task SetCategoriesAsync(int page, int pageSize, CategoryListResponse response,
        CancellationToken cancellationToken)
    {
        CategorySets++;
        CategoryResponse = response;
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(CancellationToken cancellationToken)
    {
        Invalidations++;
        ProductResponse = null;
        CategoryResponse = null;
        return Task.CompletedTask;
    }
}

internal sealed class CatalogRatingsReader : IProductRatingsReader
{
    public ProductRatingsSnapshot? Snapshot { get; set; }
    public bool Fail { get; set; }

    public Task<ProductRatingsSnapshot?> GetForProductAsync(string productId,
        CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        return Task.FromResult(Snapshot);
    }
}

internal sealed class CatalogSellerNames : ISellerDisplayNames
{
    public Dictionary<string, string> Names { get; } = new(StringComparer.Ordinal);
    public bool Fail { get; set; }

    public Task<IReadOnlyDictionary<string, string>> FindDisplayNamesAsync(
        IReadOnlyCollection<string> sellerIds, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        IReadOnlyDictionary<string, string> names = sellerIds
            .Where(sellerId => Names.ContainsKey(sellerId))
            .ToDictionary(sellerId => sellerId, sellerId => Names[sellerId], StringComparer.Ordinal);
        return Task.FromResult(names);
    }
}

internal static class CatalogFixtures
{
    public const string ProductId = "507f1f77bcf86cd799439021";
    public const string CategoryId = "507f1f77bcf86cd799439031";
    public const string SellerId = "507f1f77bcf86cd799439011";
    public const string OtherSellerId = "507f1f77bcf86cd799439041";

    public static ProductRecord Product(string sellerId = SellerId, string status = "Active",
        decimal price = 55.50m, int stock = 10, long version = 1) =>
        new(ProductId, sellerId, "Tacos de pastor", "Tacos con piña", ["Piña", "Pastor"], CategoryId,
            price, stock, status, version, new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc));

    public static CategoryRecord Category(bool isActive = true) =>
        new(CategoryId, "Comida", isActive, 1);

    public static ProductListQuery Query(string? search = null, int page = 1, int pageSize = 20,
        string sortBy = "createdAtDesc") =>
        new(search, null, null, null, page, pageSize, sortBy);
}
