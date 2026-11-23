using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Application;

namespace UtttGo.IntegrationTests;

internal sealed class CatalogProductRepository : IProductRepository
{
    public Dictionary<string, ProductRecord> Products { get; } = new(StringComparer.Ordinal);
    public int ListCalls { get; private set; }
    public int CreateCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public bool Fail { get; set; }

    public Task<ProductPage> ListAsync(ProductListQuery query, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        ListCalls++;
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
        Products.TryGetValue(productId, out var product);
        return Task.FromResult(product);
    }

    public Task<ProductRecord> CreateAsync(NewProduct product, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        CreateCalls++;
        var record = new ProductRecord(CatalogFixtures.ProductId, product.SellerId, product.Name,
            product.Description, product.Ingredients, product.CategoryId, product.Price, product.Stock,
            ProductAvailability.Active, 0, DateTime.UtcNow, DateTime.UtcNow, product.MediaAssetId);
        Products[record.ProductId] = record;
        return Task.FromResult(record);
    }

    public Task<ProductUpdateResult> UpdateOwnAsync(string sellerId, string productId, long expectedVersion,
        ProductChanges changes, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        UpdateCalls++;
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
        Products[productId] = updated;
        return Task.FromResult(new ProductUpdateResult(ProductUpdateOutcome.Updated, updated));
    }

    public Task<ProductUpdateResult> UpdateAvailabilityAsync(string sellerId, string productId,
        long expectedVersion, string status, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        UpdateCalls++;
        if (!Products.TryGetValue(productId, out var product) || product.SellerId != sellerId)
            return Task.FromResult(new ProductUpdateResult(ProductUpdateOutcome.Missing));
        if (product.Version != expectedVersion)
            return Task.FromResult(new ProductUpdateResult(ProductUpdateOutcome.Conflict));

        var updated = product with { Status = status, Version = product.Version + 1, UpdatedAt = DateTime.UtcNow };
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
    public bool Allowed { get; set; } = true;
    public bool Fail { get; set; }

    public Task<bool> IsAllowedForPublicReadAsync(CancellationToken cancellationToken) => Check();

    public Task<bool> IsAllowedForWriteAsync(string sellerId, CancellationToken cancellationToken) => Check();

    private Task<bool> Check() =>
        Fail ? throw new InvalidOperationException("internal-secret") : Task.FromResult(Allowed);
}

internal sealed class CatalogRatingsReader : IProductRatingsReader
{
    public Task<ProductRatingsSnapshot?> GetForProductAsync(string productId,
        CancellationToken cancellationToken) => Task.FromResult<ProductRatingsSnapshot?>(null);
}

internal sealed class CatalogSellerNames : ISellerDisplayNames
{
    public Dictionary<string, string> Names { get; } = new(StringComparer.Ordinal);

    public Task<IReadOnlyDictionary<string, string>> FindDisplayNamesAsync(
        IReadOnlyCollection<string> sellerIds, CancellationToken cancellationToken)
    {
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

    public static ProductRecord Product(string sellerId = SellerId, string status = "Active") =>
        new(ProductId, sellerId, "Tacos de pastor", "Tacos con piña", ["Piña", "Pastor"], CategoryId,
            55.50m, 10, status, 1, new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc));

    public static CategoryRecord Category(bool isActive = true) =>
        new(CategoryId, "Comida", isActive, 1);

    public static LocalLoginProfile Seller() =>
        new("local-id", "sub-1", "a@uttt.edu.mx", "Active", ["Buyer", "Seller"], "Alumna", "1234567890", null,
            3, SellerId);
}
