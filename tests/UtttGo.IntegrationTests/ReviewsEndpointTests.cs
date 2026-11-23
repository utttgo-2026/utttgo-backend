using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Reviews.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class ReviewsEndpointTests
{
    private const string OrderId = "665f1d408b3f2a0012ab34ce";
    private const string SellerId = "665f1a108b3f2a0012ab34c1";
    private const string ProductId = "665f1c2e8b3f2a0012ab34cd";

    [Fact]
    public async Task BuyerCanCreateOneReviewForDeliveredOrder()
    {
        var reviews = new ReviewsRepositoryDouble();
        using var factory = Factory(reviews: reviews);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");

        using var response = await client.PostAsJsonAsync($"/api/v1/orders/{OrderId}/review",
            new { rating = 5, comment = "Excelente" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("sellerId").GetString().Should().Be(SellerId);
        body.RootElement.GetProperty("productId").GetString().Should().Be(ProductId);
        body.RootElement.GetProperty("rating").GetInt32().Should().Be(5);
        reviews.Reviews.Should().ContainSingle();

        using var duplicate = await client.PostAsJsonAsync($"/api/v1/orders/{OrderId}/review",
            new { rating = 4 });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PendingOrderCannotReceiveReview()
    {
        using var factory = Factory(delivered: false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");

        using var response = await client.PostAsJsonAsync($"/api/v1/orders/{OrderId}/review",
            new { rating = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SellerReviewsReturnMetricsAndOnlyThatSeller()
    {
        var reviews = new ReviewsRepositoryDouble();
        reviews.Reviews.AddRange([
            new("r1", "o1", "buyer-1", SellerId, ProductId, 5, "great", DateTime.UtcNow.AddMinutes(-2)),
            new("r2", "o2", "buyer-2", SellerId, ProductId, 3, null, DateTime.UtcNow.AddMinutes(-1)),
            new("r3", "o3", "buyer-3", "other-seller", ProductId, 1, "other", DateTime.UtcNow)
        ]);
        using var factory = Factory(reviews: reviews);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/sellers/{SellerId}/reviews?page=1&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(2);
        body.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        body.RootElement.GetProperty("averageRating").GetDecimal().Should().Be(4m);
        body.RootElement.GetProperty("ratingBreakdown").GetProperty("five").GetInt64().Should().Be(1);
        body.RootElement.GetProperty("ratingBreakdown").GetProperty("three").GetInt64().Should().Be(1);
        body.RootElement.GetProperty("items")[0].EnumerateObject().Select(item => item.Name)
            .Should().BeEquivalentTo(["reviewId", "productId", "rating", "comment", "createdAt"]);
    }

    [Fact]
    public async Task InvalidReviewRatingIsRejected()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");

        using var response = await client.PostAsJsonAsync($"/api/v1/orders/{OrderId}/review",
            new { rating = 6 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
    }

    private static WebApplicationFactory<Program> Factory(ReviewsRepositoryDouble? reviews = null,
        bool delivered = true, ReviewsRateLimitDouble? gate = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDeliveredOrderReader>();
            services.RemoveAll<IReviewRepository>();
            services.RemoveAll<IReviewsRateLimitGate>();
            services.RemoveAll<IAuthenticatedLocalProfileReader>();
            services.AddSingleton<IDeliveredOrderReader>(new DeliveredOrderReaderDouble
            {
                Snapshot = delivered
                    ? new(OrderId, SellerFixtures.UserId, SellerId, ProductId, "Delivered", DateTime.UtcNow)
                    : null
            });
            services.AddSingleton<IReviewRepository>(reviews ?? new ReviewsRepositoryDouble());
            services.AddSingleton<IReviewsRateLimitGate>(gate ?? new ReviewsRateLimitDouble());
            services.AddSingleton<IAuthenticatedLocalProfileReader>(new AuthenticatedProfileDouble
            {
                Profile = SellerFixtures.Profile()
            });
        }));

    private sealed class DeliveredOrderReaderDouble : IDeliveredOrderReader
    {
        public DeliveredOrderSnapshot? Snapshot { get; set; }
        public Task<DeliveredOrderSnapshot?> FindDeliveredForBuyerAsync(string orderId, string buyerUserId,
            CancellationToken cancellationToken) => Task.FromResult(Snapshot is { OrderId: var id, BuyerUserId: var buyer }
            && id == orderId && buyer == buyerUserId ? Snapshot : null);
    }

    private sealed class ReviewsRepositoryDouble : IReviewRepository
    {
        public List<ReviewRecord> Reviews { get; } = [];
        public Task<ReviewRecord> CreateAsync(ReviewRecord review, CancellationToken cancellationToken)
        {
            if (Reviews.Any(item => item.OrderId == review.OrderId)) throw new DuplicateReviewException();
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
        private static ReviewRatings Ratings(IEnumerable<ReviewRecord> source)
        {
            var values = source.ToList();
            return new(values.Count == 0 ? null : Math.Round((decimal)values.Sum(item => item.Rating) / values.Count, 2),
                values.Count, Enumerable.Range(1, 5).ToDictionary(rating => rating,
                    rating => (long)values.Count(item => item.Rating == rating)));
        }
    }

    private sealed class ReviewsRateLimitDouble : IReviewsRateLimitGate
    {
        public bool Allowed { get; set; } = true;
        public Task<bool> IsAllowedForCreateAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(Allowed);
        public Task<bool> IsAllowedForReadAsync(CancellationToken cancellationToken) => Task.FromResult(Allowed);
    }
}
