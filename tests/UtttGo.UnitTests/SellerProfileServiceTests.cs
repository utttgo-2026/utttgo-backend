using FluentAssertions;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Modules.Sellers.Contracts;
using UTTTGO.Api.Modules.Sellers.Validators;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class SellerProfileServiceTests
{
    private const string Id = "507f1f77bcf86cd799439011";
    private static readonly SellerProfileSnapshot Seller = new(Id, "Campus Snacks", "Active");

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Inactive", false)]
    public async Task KnownSellerIsVisibleWithoutInventedRatings(string status, bool active)
    {
        var result = await Service(seller: Seller with { Status = status }).GetAsync(Id, default);
        result.Outcome.Should().Be(SellerProfileOutcome.Found);
        result.Response!.SellerId.Should().Be(Id);
        result.Response.DisplayName.Should().Be("Campus Snacks");
        result.Response.IsActive.Should().Be(active);
        result.Response.RatingAverage.Should().BeNull();
        result.Response.RatingCount.Should().BeNull();
    }

    [Fact]
    public async Task ReviewedSellerUsesReviewsContractOnly()
    {
        var result = await Service(review: new(4.25m, 8)).GetAsync(Id, default);
        result.Response!.RatingAverage.Should().Be(4.25m);
        result.Response.RatingCount.Should().Be(8);
    }

    [Theory]
    [InlineData("Deleted")]
    [InlineData("Suspended")]
    public async Task NonPublicSellerIsHidden(string status)
    {
        var result = await Service(seller: Seller with { Status = status }).GetAsync(Id, default);
        result.Outcome.Should().Be(SellerProfileOutcome.NotFound);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task MissingSellerIs404CandidateWithoutConsultingReviews()
    {
        var reviews = new Reviews(new(4m, 2));
        var result = await new SellerProfileService(new Gate(), new Store(null), reviews).GetAsync(Id, default);
        result.Outcome.Should().Be(SellerProfileOutcome.NotFound);
        reviews.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("rate")]
    [InlineData("seller")]
    public async Task CriticalDependencyExceptionIsUnavailable(string failure)
    {
        var result = await Service(failure: failure).GetAsync(Id, default);
        result.Outcome.Should().Be(SellerProfileOutcome.Unavailable);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task RateLimitDenialSkipsSellerLookup()
    {
        var store = new Store(Seller);
        var result = await new SellerProfileService(new Gate(allowed: false), store, new Reviews(null))
            .GetAsync(Id, default);
        result.Outcome.Should().Be(SellerProfileOutcome.RateLimited);
        store.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ReviewsFailureDegradesToAbsentRatingsWithoutHidingInactiveSeller()
    {
        var result = await Service(seller: Seller with { Status = "Inactive" }, failure: "reviews")
            .GetAsync(Id, default);
        result.Outcome.Should().Be(SellerProfileOutcome.Found);
        result.Response!.IsActive.Should().BeFalse();
        result.Response.RatingCount.Should().BeNull();
        result.Response.RatingAverage.Should().BeNull();
    }

    [Theory]
    [InlineData("-1", 3)]
    [InlineData("6", 1)]
    [InlineData(null, 1)]
    public async Task InconsistentRatingsAreNotPublished(string? averageText, long count)
    {
        decimal? average = averageText is null ? null : decimal.Parse(averageText,
            System.Globalization.CultureInfo.InvariantCulture);
        var result = await Service(review: new(average, count)).GetAsync(Id, default);
        result.Response!.RatingAverage.Should().BeNull();
        result.Response.RatingCount.Should().BeNull();
    }

    [Fact]
    public async Task RatingsZeroOnlyWhenProvidedByReviews()
    {
        var result = await Service(review: new(null, 0)).GetAsync(Id, default);
        result.Response!.RatingCount.Should().Be(0);
        result.Response.RatingAverage.Should().BeNull();
    }

    [Theory]
    [InlineData("Other")]
    [InlineData("Pending")]
    public async Task UnknownSellerStateFailsClosed(string status)
    {
        var result = await Service(seller: Seller with { Status = status }).GetAsync(Id, default);
        result.Outcome.Should().Be(SellerProfileOutcome.Unavailable);
    }

    [Fact]
    public async Task MismatchedSellerIdFailsClosed()
    {
        var result = await Service(seller: Seller with { SellerId = "different" }).GetAsync(Id, default);
        result.Outcome.Should().Be(SellerProfileOutcome.Unavailable);
    }

    [Theory]
    [InlineData("507f1f77bcf86cd799439011", true)]
    [InlineData("507F1F77BCF86CD799439011", true)]
    [InlineData("", false)]
    [InlineData("507f1f77bcf86cd79943901", false)]
    [InlineData("507f1f77bcf86cd79943901g", false)]
    [InlineData("../507f1f77bcf86cd799439011", false)]
    public void IdValidatorAcceptsOnlyCandidateObjectId(string id, bool expected)
    {
        new SellerIdRequestValidator().Validate(new SellerIdRequest(id)).IsValid.Should().Be(expected);
    }

    private static SellerProfileService Service(SellerProfileSnapshot? seller = null,
        SellerRatingsSnapshot? review = null, string? failure = null) =>
        new(new Gate(fail: failure == "rate"), new Store(seller ?? Seller, failure == "seller"),
            new Reviews(review, failure == "reviews"));

    private sealed class Gate(bool allowed = true, bool fail = false) : IPublicSellerRateLimitGate
    {
        public Task<bool> IsAllowedAsync(string sellerId, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException() : Task.FromResult(allowed);
    }

    private sealed class Store(SellerProfileSnapshot? seller, bool fail = false) : ISellerProfileRepository
    {
        public int Calls { get; private set; }
        public Task<SellerProfileSnapshot?> FindByIdAsync(string sellerId, CancellationToken cancellationToken)
        {
            Calls++;
            return fail ? throw new InvalidOperationException() : Task.FromResult(seller);
        }
    }

    private sealed class Reviews(SellerRatingsSnapshot? review, bool fail = false) : ISellerRatingsReader
    {
        public int Calls { get; private set; }
        public Task<SellerRatingsSnapshot?> GetForSellerAsync(string sellerId, CancellationToken cancellationToken)
        {
            Calls++;
            return fail ? throw new InvalidOperationException() : Task.FromResult(review);
        }
    }
}
