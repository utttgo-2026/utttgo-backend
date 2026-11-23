using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Reviews.Application;
using UTTTGO.Api.Modules.Reviews.Contracts;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class ReviewServiceTests
{
    private const string OrderId = "665f1d408b3f2a0012ab34ce";
    private const string SellerId = "665f1a108b3f2a0012ab34c1";
    private const string ProductId = "665f1c2e8b3f2a0012ab34cd";

    [Fact]
    public async Task BuyerCanReviewDeliveredOrderOnlyOnce()
    {
        var orders = new DeliveredOrderReaderDouble
        {
            Snapshot = new(OrderId, SellerFixtures.UserId, SellerId, ProductId, "Delivered",
                DateTime.UtcNow)
        };
        var reviews = new ReviewsRepositoryDouble();
        var service = CreateService(orders, reviews);

        var result = await service.CreateAsync("token", OrderId,
            new CreateReviewRequest { Rating = 5, Comment = "Excelente" }, CancellationToken.None);
        var duplicate = await service.CreateAsync("token", OrderId,
            new CreateReviewRequest { Rating = 4 }, CancellationToken.None);

        result.Outcome.Should().Be(CreateReviewOutcome.Created);
        result.Response!.SellerId.Should().Be(SellerId);
        result.Response.ProductId.Should().Be(ProductId);
        duplicate.Outcome.Should().Be(CreateReviewOutcome.AlreadyExists);
        reviews.Reviews.Should().ContainSingle();
    }

    [Fact]
    public async Task PendingOrderCannotBeReviewed()
    {
        var orders = new DeliveredOrderReaderDouble();
        var reviews = new ReviewsRepositoryDouble();
        var result = await CreateService(orders, reviews).CreateAsync("token", OrderId,
            new CreateReviewRequest { Rating = 5 }, CancellationToken.None);

        result.Outcome.Should().Be(CreateReviewOutcome.NotFound);
        reviews.Reviews.Should().BeEmpty();
    }

    [Fact]
    public async Task SellerReviewsArePaginatedWithBreakdown()
    {
        var reviews = new ReviewsRepositoryDouble();
        reviews.Reviews.AddRange([
            new("r1", "o1", "b1", SellerId, ProductId, 5, "a", DateTime.UtcNow.AddMinutes(-2)),
            new("r2", "o2", "b2", SellerId, ProductId, 3, null, DateTime.UtcNow.AddMinutes(-1)),
            new("r3", "o3", "b3", "other", ProductId, 1, "no", DateTime.UtcNow)
        ]);

        var result = await new ListSellerReviewsService(new ReviewsRateLimitDouble(), reviews)
            .ListAsync(SellerId, new SellerReviewsQuery(1, 1), CancellationToken.None);

        result.Outcome.Should().Be(ListSellerReviewsOutcome.Success);
        result.Response!.TotalCount.Should().Be(2);
        result.Response.Items.Should().ContainSingle();
        result.Response.AverageRating.Should().Be(4m);
        result.Response.RatingBreakdown.One.Should().Be(0);
        result.Response.RatingBreakdown.Three.Should().Be(1);
        result.Response.RatingBreakdown.Five.Should().Be(1);
    }

    private static CreateReviewService CreateService(DeliveredOrderReaderDouble orders,
        ReviewsRepositoryDouble reviews, AuthenticatedProfileDouble? profile = null,
        ReviewsRateLimitDouble? gate = null) => new(profile ?? new AuthenticatedProfileDouble
        {
            Profile = SellerFixtures.Buyer()
        }, orders, gate ?? new ReviewsRateLimitDouble(), reviews,
        NullLogger<CreateReviewService>.Instance);
}
