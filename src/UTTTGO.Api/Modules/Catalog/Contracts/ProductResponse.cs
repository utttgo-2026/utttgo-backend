namespace UTTTGO.Api.Modules.Catalog.Contracts;

// Public projection of a product: commercial data of the product and its seller only.
// No seller contact data, no internal identifiers of other modules and no ratings that
// Reviews did not publish.
// imageUrl is a short-lived presigned GET URL generated on each catalog read for the
// approved/ready Media asset, or null when the product has no readable photo (BR-007).
public sealed record ProductResponse(string Id, string SellerId, string? SellerDisplayName, string Name,
    string Description, IReadOnlyList<string> Ingredients, string CategoryId, string? CategoryName,
    decimal Price, int Stock, string Status, bool IsAvailable, decimal? RatingAverage, long? RatingCount,
    DateTime CreatedAt, DateTime UpdatedAt, string? ImageUrl = null);

public sealed record ProductListResponse(IReadOnlyList<ProductResponse> Items, int Page, int PageSize,
    int TotalCount);

public sealed record CategoryResponse(string Id, string Name, int SortOrder);

public sealed record CategoryListResponse(IReadOnlyList<CategoryResponse> Items, int Page, int PageSize,
    int TotalCount);
