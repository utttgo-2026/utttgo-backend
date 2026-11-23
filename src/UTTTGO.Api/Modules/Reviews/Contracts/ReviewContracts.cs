namespace UTTTGO.Api.Modules.Reviews.Contracts;

public sealed class CreateReviewRequest
{
    public int? Rating { get; set; }
    public string? Comment { get; set; }
}

public sealed record ReviewResponse(string ReviewId, string SellerId, string ProductId, int Rating,
    string? Comment, DateTime CreatedAt);

public sealed record SellerReviewItem(string ReviewId, string ProductId, int Rating, string? Comment,
    DateTime CreatedAt);

public sealed record RatingBreakdown(long One, long Two, long Three, long Four, long Five);

public sealed record SellerReviewsResponse(IReadOnlyList<SellerReviewItem> Items, int Page, int PageSize,
    int TotalCount, decimal? AverageRating, RatingBreakdown RatingBreakdown);

public sealed record SellerReviewsQuery(int Page, int PageSize)
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPage = 1000;
    public const int MaxPageSize = 50;
}
