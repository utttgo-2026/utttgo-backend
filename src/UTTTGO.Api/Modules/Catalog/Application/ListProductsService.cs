using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Media.Application;
using UTTTGO.Api.Modules.Sellers.Application;

namespace UTTTGO.Api.Modules.Catalog.Application;

public interface IListProductsService
{
    Task<ProductListResult> ListAsync(ProductListQuery query, CancellationToken cancellationToken);
}

public enum ProductListOutcome { Success, RateLimited, Unavailable }

public sealed record ProductListResult(ProductListOutcome Outcome, ProductListResponse? Response = null);

public sealed class ListProductsService(ICatalogRateLimitGate rateLimit, IProductRepository products,
    ICategoryRepository categories, IProductRatingsReader ratings, ISellerDisplayNames sellers,
    IProductImageGuard images)
    : IListProductsService
{
    public async Task<ProductListResult> ListAsync(ProductListQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await rateLimit.IsAllowedForPublicReadAsync(cancellationToken))
                return new(ProductListOutcome.RateLimited);

            var activeCategories = await categories.ListActiveAsync(cancellationToken);
            var effective = query with { AllowedCategoryIds = activeCategories.Select(item => item.CategoryId).ToList() };
            var page = await products.ListAsync(effective, cancellationToken);
            if (page.Items.Count == 0) return new(ProductListOutcome.Success,
                ProductProjection.ToListResponse([], query, page.Total));

            var categoryNames = activeCategories.ToDictionary(item => item.CategoryId,
                item => item.Name, StringComparer.Ordinal);
            var sellerIds = page.Items.Select(item => item.SellerId).Distinct(StringComparer.Ordinal).ToList();
            var sellerNames = await sellers.FindDisplayNamesAsync(sellerIds, cancellationToken);
            var imageReferences = page.Items
                .Where(item => item.ImageAssetId is not null && item.ImageObjectKey is not null)
                .Select(item => new MediaAssetReference(item.ImageAssetId!, item.ImageObjectKey!))
                .ToList();
            var imageUrls = await images.GetReadUrlsAsync(imageReferences, cancellationToken);

            var items = new List<ProductResponse>(page.Items.Count);
            foreach (var product in page.Items)
            {
                var rating = await ReadRatingsAsync(product.ProductId, cancellationToken);
                items.Add(ProductProjection.ToResponse(product,
                    sellerNames.TryGetValue(product.SellerId, out var displayName) ? displayName : null,
                    categoryNames.TryGetValue(product.CategoryId, out var categoryName) ? categoryName : null,
                    rating,
                    product.ImageAssetId is not null && imageUrls.TryGetValue(product.ImageAssetId, out var imageUrl)
                        ? imageUrl : null));
            }

            return new(ProductListOutcome.Success, ProductProjection.ToListResponse(items, query, page.Total));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Product storage, categories and the distributed rate limit are critical: fail closed.
            return new(ProductListOutcome.Unavailable);
        }
    }

    private async Task<ProductRatingsSnapshot?> ReadRatingsAsync(string productId,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await ratings.GetForProductAsync(productId, cancellationToken);
            if (snapshot is null) return null;
            if (snapshot.Count < 0) return null;
            if (snapshot.Count == 0 && snapshot.Average is null) return snapshot;
            if (snapshot.Count > 0 && snapshot.Average is >= 0 and <= 5) return snapshot;
            return null;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Reviews is optional for catalog reads; omit the ratings when it is unavailable.
            return null;
        }
    }
}
