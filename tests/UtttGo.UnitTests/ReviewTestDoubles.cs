using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Reviews.Application;

namespace UtttGo.UnitTests;

internal sealed class DeliveredOrderReaderDouble : IDeliveredOrderReader
{
    public DeliveredOrderSnapshot? Snapshot { get; set; }
    public bool Fail { get; set; }

    public Task<DeliveredOrderSnapshot?> FindDeliveredForBuyerAsync(string orderId, string buyerUserId,
        CancellationToken cancellationToken) => Fail
        ? throw new InvalidOperationException("internal-secret")
        : Task.FromResult(Snapshot is { OrderId: var id, BuyerUserId: var buyer } && id == orderId && buyer == buyerUserId
            ? Snapshot
            : null);
}

internal sealed class ReviewsRepositoryDouble : IReviewRepository
{
    public List<ReviewRecord> Reviews { get; } = [];
    public bool Fail { get; set; }
    public bool ForceDuplicate { get; set; }

    public Task<ReviewRecord> CreateAsync(ReviewRecord review, CancellationToken cancellationToken)
    {
        if (Fail) throw new InvalidOperationException("internal-secret");
        if (ForceDuplicate || Reviews.Any(item => item.OrderId == review.OrderId))
            throw new DuplicateReviewException();
        var created = review with { ReviewId = $"review-{Reviews.Count + 1}" };
        Reviews.Add(created);
        return Task.FromResult(created);
    }

    public Task<bool> ExistsForOrderAsync(string orderId, CancellationToken cancellationToken) =>
        Task.FromResult(Reviews.Any(item => item.OrderId == orderId));

    public Task<ReviewPage> ListForSellerAsync(string sellerId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var all = Reviews.Where(item => item.SellerId == sellerId).OrderByDescending(item => item.CreatedAt)
            .ToList();
        return Task.FromResult(new ReviewPage(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), all.Count));
    }

    public Task<ReviewRatings> GetRatingsForSellerAsync(string sellerId, CancellationToken cancellationToken) =>
        Task.FromResult(Ratings(Reviews.Where(item => item.SellerId == sellerId)));

    public Task<ReviewRatings> GetRatingsForProductAsync(string productId, CancellationToken cancellationToken) =>
        Task.FromResult(Ratings(Reviews.Where(item => item.ProductId == productId)));

    private static ReviewRatings Ratings(IEnumerable<ReviewRecord> reviews)
    {
        var values = reviews.ToList();
        return new(values.Count == 0 ? null : Math.Round((decimal)values.Sum(item => item.Rating) / values.Count, 2),
            values.Count, Enumerable.Range(1, 5).ToDictionary(rating => rating,
                rating => (long)values.Count(item => item.Rating == rating)));
    }
}

internal sealed class ReviewsRateLimitDouble : IReviewsRateLimitGate
{
    public bool Allowed { get; set; } = true;
    public bool Fail { get; set; }

    public Task<bool> IsAllowedForCreateAsync(string userId, CancellationToken cancellationToken) => Check();
    public Task<bool> IsAllowedForReadAsync(CancellationToken cancellationToken) => Check();

    private Task<bool> Check() => Fail
        ? throw new InvalidOperationException("internal-secret")
        : Task.FromResult(Allowed);
}
