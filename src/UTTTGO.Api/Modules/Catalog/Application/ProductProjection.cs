using UTTTGO.Api.Modules.Catalog.Contracts;
using UTTTGO.Api.Shared.Contracts;
using UTTTGO.Api.Modules.Media.Application;

namespace UTTTGO.Api.Modules.Catalog.Application;

public static class ProductProjection
{
    public static ProductResponse ToResponse(ProductRecord product, string? sellerDisplayName,
        string? categoryName, ProductRatingsSnapshot? ratings, MediaReadUrl? image = null) =>
        new(product.ProductId, product.SellerId, sellerDisplayName, product.Name, product.Description,
            product.Ingredients, product.CategoryId, categoryName, product.Price, product.Stock,
            product.Status, product.Status == ProductAvailability.Active && product.Stock > 0,
            ratings?.Average, ratings?.Count, product.CreatedAt, product.UpdatedAt, image?.Url, image?.ExpiresAt);

    // Only the public projection is published; internal fields never reach the client.
    public static ProductListResponse ToListResponse(IReadOnlyList<ProductResponse> items,
        ProductListQuery query, long total) =>
        new(items, query.Page, query.PageSize, PaginationCount.FromCount(total));
}
