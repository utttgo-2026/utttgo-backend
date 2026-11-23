using UTTTGO.Api.Modules.Reviews.Contracts;

namespace UTTTGO.Api.Modules.Reviews.Application;

public interface IReviewRepository
{
    Task<ReviewRecord> CreateAsync(ReviewRecord review, CancellationToken cancellationToken);
    Task<bool> ExistsForOrderAsync(string orderId, CancellationToken cancellationToken);
    Task<ReviewPage> ListForSellerAsync(string sellerId, int page, int pageSize,
        CancellationToken cancellationToken);
    Task<ReviewRatings> GetRatingsForSellerAsync(string sellerId, CancellationToken cancellationToken);
    Task<ReviewRatings> GetRatingsForProductAsync(string productId, CancellationToken cancellationToken);
}

public interface IReviewsRateLimitGate
{
    Task<bool> IsAllowedForCreateAsync(string userId, CancellationToken cancellationToken);
    Task<bool> IsAllowedForReadAsync(CancellationToken cancellationToken);
}

public sealed record ReviewRecord(string ReviewId, string OrderId, string BuyerUserId, string SellerId,
    string ProductId, int Rating, string? Comment, DateTime CreatedAt);

public sealed record ReviewPage(IReadOnlyList<ReviewRecord> Items, long Total);

public sealed record ReviewRatings(decimal? Average, long Count, IReadOnlyDictionary<int, long> Breakdown);
