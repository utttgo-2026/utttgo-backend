using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Modules.Media.Application;

namespace UTTTGO.Api.Modules.Catalog.Application;

public interface IGetProductService
{
    Task<GetProductResult> GetAsync(string productId, CancellationToken cancellationToken);
}

public enum GetProductOutcome { Found, NotFound, RateLimited, Unavailable }

public sealed record GetProductResult(GetProductOutcome Outcome, ProductResponse? Response = null);

public sealed class GetProductService(ICatalogRateLimitGate rateLimit, IProductRepository products,
    ICategoryRepository categories, IProductRatingsReader ratings, ISellerDisplayNames sellers,
    IProductMediaAccess? media = null)
    : IGetProductService
{
    public async Task<GetProductResult> GetAsync(string productId, CancellationToken cancellationToken)
    {
        try
        {
            if (!await rateLimit.IsAllowedForPublicReadAsync(cancellationToken))
                return new(GetProductOutcome.RateLimited);

            var product = await products.FindByIdAsync(productId, cancellationToken);
            if (product is null || !ProductAvailability.IsPublic(product.Status))
                return new(GetProductOutcome.NotFound);

            var category = await categories.FindByIdAsync(product.CategoryId, cancellationToken);
            if (category is null || !category.IsActive) return new(GetProductOutcome.NotFound);

            var sellerNames = await sellers.FindDisplayNamesAsync([product.SellerId], cancellationToken);
            var rating = await ReadRatingsAsync(product.ProductId, cancellationToken);
            var image = product.MediaAssetId is { } id && media is not null
                ? await media.GetApprovedReadUrlAsync(id, cancellationToken) : null;
            var response = ProductProjection.ToResponse(product,
                sellerNames.TryGetValue(product.SellerId, out var displayName) ? displayName : null,
                category.Name, rating, image);
            return new(GetProductOutcome.Found, response);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(GetProductOutcome.Unavailable);
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
            return null;
        }
    }
}
