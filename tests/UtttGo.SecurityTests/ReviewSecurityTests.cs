using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Reviews.Application;
using UTTTGO.Api.Modules.Reviews.Contracts;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class ReviewSecurityTests
{
    private const string OrderId = "665f1d408b3f2a0012ab34ce";
    private const string SellerId = "665f1a108b3f2a0012ab34c1";
    private const string ProductId = "665f1c2e8b3f2a0012ab34cd";

    [Fact]
    public async Task AnotherBuyerCannotReviewTheOrder()
    {
        var service = CreateService(new DeliveredOrderReaderDouble
        {
            Snapshot = new(OrderId, "different-buyer", SellerId, ProductId, "Delivered", DateTime.UtcNow)
        });

        var result = await service.CreateAsync("token", OrderId,
            new CreateReviewRequest { Rating = 5 }, CancellationToken.None);

        result.Outcome.Should().Be(CreateReviewOutcome.NotFound);
    }

    [Fact]
    public async Task RevokedOrInvalidSessionCannotReview()
    {
        var service = CreateService(new DeliveredOrderReaderDouble
        {
            Snapshot = new(OrderId, "local-id", SellerId, ProductId, "Delivered", DateTime.UtcNow)
        }, new AuthenticatedProfileDouble
        {
            Outcome = AuthenticatedLocalProfileOutcome.Unauthorized
        });

        var result = await service.CreateAsync("revoked-token", OrderId,
            new CreateReviewRequest { Rating = 5 }, CancellationToken.None);

        result.Outcome.Should().Be(CreateReviewOutcome.Unauthorized);
    }

    [Fact]
    public async Task RedisRateLimitDenialPreventsReviewCreation()
    {
        var reviews = new SecurityReviewsRepositoryDouble();
        var service = CreateService(new DeliveredOrderReaderDouble
        {
            Snapshot = new(OrderId, "local-id", SellerId, ProductId, "Delivered", DateTime.UtcNow)
        }, gate: new SecurityReviewsRateLimitDouble { Allowed = false }, reviews: reviews);

        var result = await service.CreateAsync("token", OrderId,
            new CreateReviewRequest { Rating = 5 }, CancellationToken.None);

        result.Outcome.Should().Be(CreateReviewOutcome.RateLimited);
        reviews.Created.Should().Be(0);
    }

    private static CreateReviewService CreateService(DeliveredOrderReaderDouble orders,
        AuthenticatedProfileDouble? profile = null, SecurityReviewsRateLimitDouble? gate = null,
        SecurityReviewsRepositoryDouble? reviews = null) => new(profile ?? new AuthenticatedProfileDouble
        {
            Profile = SellerFixtures.Profile()
        }, orders, gate ?? new SecurityReviewsRateLimitDouble(), reviews ?? new SecurityReviewsRepositoryDouble(),
        NullLogger<CreateReviewService>.Instance);

    private sealed class DeliveredOrderReaderDouble : IDeliveredOrderReader
    {
        public DeliveredOrderSnapshot? Snapshot { get; set; }
        public Task<DeliveredOrderSnapshot?> FindDeliveredForBuyerAsync(string orderId, string buyerUserId,
            CancellationToken cancellationToken) => Task.FromResult(Snapshot is { OrderId: var id, BuyerUserId: var buyer }
            && id == orderId && buyer == buyerUserId ? Snapshot : null);
    }

    private sealed class SecurityReviewsRepositoryDouble : IReviewRepository
    {
        public int Created { get; private set; }
        public Task<ReviewRecord> CreateAsync(ReviewRecord review, CancellationToken cancellationToken)
        {
            Created++;
            return Task.FromResult(review with { ReviewId = "review-1" });
        }
        public Task<bool> ExistsForOrderAsync(string orderId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<ReviewPage> ListForSellerAsync(string sellerId, int page, int pageSize,
            CancellationToken cancellationToken) => Task.FromResult(new ReviewPage([], 0));
        public Task<ReviewRatings> GetRatingsForSellerAsync(string sellerId, CancellationToken cancellationToken) =>
            Task.FromResult(new ReviewRatings(null, 0, new Dictionary<int, long>()));
        public Task<ReviewRatings> GetRatingsForProductAsync(string productId, CancellationToken cancellationToken) =>
            Task.FromResult(new ReviewRatings(null, 0, new Dictionary<int, long>()));
    }

    private sealed class SecurityReviewsRateLimitDouble : IReviewsRateLimitGate
    {
        public bool Allowed { get; set; } = true;
        public Task<bool> IsAllowedForCreateAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(Allowed);
        public Task<bool> IsAllowedForReadAsync(CancellationToken cancellationToken) => Task.FromResult(Allowed);
    }
}
