using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Reviews.Contracts;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Shared.Contracts;

namespace UTTTGO.Api.Modules.Reviews.Application;

public interface IListSellerReviewsService
{
    Task<ListSellerReviewsResult> ListAsync(string sellerId, SellerReviewsQuery query,
        CancellationToken cancellationToken);
}

public enum ListSellerReviewsOutcome { Success, RateLimited, Unavailable }

public sealed record ListSellerReviewsResult(ListSellerReviewsOutcome Outcome,
    SellerReviewsResponse? Response = null);

public sealed class ListSellerReviewsService(IReviewsRateLimitGate rateLimit, IReviewRepository reviews)
    : IListSellerReviewsService, ISellerRatingsReader, IProductRatingsReader
{
    public async Task<ListSellerReviewsResult> ListAsync(string sellerId, SellerReviewsQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await rateLimit.IsAllowedForReadAsync(cancellationToken))
                return new(ListSellerReviewsOutcome.RateLimited);
            var page = await reviews.ListForSellerAsync(sellerId, query.Page, query.PageSize, cancellationToken);
            var ratings = await reviews.GetRatingsForSellerAsync(sellerId, cancellationToken);
            return new(ListSellerReviewsOutcome.Success, new(
                page.Items.Select(review => new SellerReviewItem(review.ReviewId, review.ProductId,
                    review.Rating, review.Comment, review.CreatedAt)).ToList(), query.Page, query.PageSize,
                PaginationCount.FromCount(page.Total), ratings.Average, ToBreakdown(ratings.Breakdown)));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ListSellerReviewsOutcome.Unavailable);
        }
    }

    public async Task<SellerRatingsSnapshot?> GetForSellerAsync(string sellerId,
        CancellationToken cancellationToken)
    {
        var ratings = await reviews.GetRatingsForSellerAsync(sellerId, cancellationToken);
        return new(ratings.Average, ratings.Count);
    }

    public async Task<ProductRatingsSnapshot?> GetForProductAsync(string productId,
        CancellationToken cancellationToken)
    {
        var ratings = await reviews.GetRatingsForProductAsync(productId, cancellationToken);
        return new(ratings.Average, ratings.Count);
    }

    private static RatingBreakdown ToBreakdown(IReadOnlyDictionary<int, long> breakdown) =>
        new(Get(breakdown, 1), Get(breakdown, 2), Get(breakdown, 3), Get(breakdown, 4), Get(breakdown, 5));

    private static long Get(IReadOnlyDictionary<int, long> breakdown, int rating) =>
        breakdown.TryGetValue(rating, out var count) ? count : 0;
}
