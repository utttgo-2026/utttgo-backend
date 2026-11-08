using FluentAssertions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class OwnSellerProfileServiceTests
{
    [Fact]
    public async Task SellerReadsOwnBusinessProfileWithoutPrivateAccountFields()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();

        var result = await Service(writer).GetAsync("token", default);

        result.Outcome.Should().Be(OwnSellerProfileOutcome.Found);
        result.Response!.SellerId.Should().Be(SellerFixtures.SellerId);
        result.Response.BusinessName.Should().Be("Campus Snacks");
        result.Response.IsActive.Should().BeTrue();
        result.Response.Roles.Should().Contain("Seller");
    }

    [Fact]
    public async Task AuthenticatedBuyerWithoutSellerIsNotFound()
    {
        var writer = new InMemorySellerWriter();
        var result = await Service(writer).GetAsync("token", default);

        result.Outcome.Should().Be(OwnSellerProfileOutcome.NotSeller);
        result.Response.Should().BeNull();
    }

    [Theory]
    [InlineData(AuthenticatedLocalProfileOutcome.Unauthorized)]
    [InlineData(AuthenticatedLocalProfileOutcome.ProfileMissing)]
    public async Task MissingOrInvalidSessionIsUnauthorized(AuthenticatedLocalProfileOutcome outcome)
    {
        var reader = new AuthenticatedProfileDouble { Outcome = outcome };
        var result = await Service(new InMemorySellerWriter(), reader).GetAsync("token", default);

        result.Outcome.Should().Be(OwnSellerProfileOutcome.Unauthorized);
    }

    [Fact]
    public async Task RateLimitDenialSkipsTheSellerLookup()
    {
        var writer = new InMemorySellerWriter();
        var limit = new SellerRateLimitDouble { Allowed = false };

        var result = await Service(writer, limit: limit).GetAsync("token", default);

        result.Outcome.Should().Be(OwnSellerProfileOutcome.RateLimited);
        writer.Sellers.Should().BeEmpty();
    }

    [Fact]
    public async Task CriticalFailureIsUnavailable()
    {
        var writer = new InMemorySellerWriter { Fail = true };
        var result = await Service(writer).GetAsync("token", default);

        result.Outcome.Should().Be(OwnSellerProfileOutcome.Unavailable);
    }

    private static OwnSellerProfileService Service(InMemorySellerWriter writer,
        AuthenticatedProfileDouble? reader = null, SellerRateLimitDouble? limit = null) =>
        new(reader ?? new AuthenticatedProfileDouble
        {
            Profile = SellerFixtures.Buyer(withSellerRole: true, sellerId: SellerFixtures.SellerId)
        }, writer, limit ?? new SellerRateLimitDouble());
}
