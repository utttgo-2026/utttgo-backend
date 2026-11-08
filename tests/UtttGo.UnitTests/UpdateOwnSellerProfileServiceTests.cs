using FluentAssertions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Modules.Sellers.Contracts;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class UpdateOwnSellerProfileServiceTests
{
    [Fact]
    public async Task OwnProfileIsUpdatedAndVersionIsIncremented()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller(version: 2);

        var result = await Service(writer).UpdateAsync("token", Request("Nuevo nombre"), default);

        result.Outcome.Should().Be(UpdateOwnSellerProfileOutcome.Updated);
        result.Response!.BusinessName.Should().Be("Nuevo nombre");
        writer.UpdateCalls.Should().Be(1);
        writer.LastOwnerUserId.Should().Be(SellerFixtures.UserId);
    }

    [Fact]
    public async Task OmittedFieldsArePreserved()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller(version: 1);

        var result = await Service(writer).UpdateAsync("token", Request("Nuevo nombre"), default);

        result.Response!.Description.Should().Be("Snacks del campus");
        result.Response.PickupPoint.Should().Be("Biblioteca central");
    }

    [Fact]
    public async Task ConcurrentUpdateIsRejectedAsConflict()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller(version: 1);
        writer.ForceConflict = true;

        var result = await Service(writer).UpdateAsync("token", Request("Nuevo nombre"), default);

        result.Outcome.Should().Be(UpdateOwnSellerProfileOutcome.Conflict);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task SellerWithoutProfileIsNotFound()
    {
        var result = await Service(new InMemorySellerWriter()).UpdateAsync("token", Request("Nuevo nombre"), default);

        result.Outcome.Should().Be(UpdateOwnSellerProfileOutcome.NotSeller);
    }

    [Fact]
    public async Task UnauthorizedSessionNeverWrites()
    {
        var writer = new InMemorySellerWriter();
        var reader = new AuthenticatedProfileDouble
        {
            Outcome = AuthenticatedLocalProfileOutcome.Unauthorized
        };

        var result = await Service(writer, reader).UpdateAsync("token", Request("Nuevo nombre"), default);

        result.Outcome.Should().Be(UpdateOwnSellerProfileOutcome.Unauthorized);
        writer.UpdateCalls.Should().Be(0);
    }

    [Fact]
    public async Task RateLimitDenialSkipsTheWrite()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();
        var limit = new SellerRateLimitDouble { Allowed = false };

        var result = await Service(writer, limit: limit).UpdateAsync("token", Request("Nuevo nombre"), default);

        result.Outcome.Should().Be(UpdateOwnSellerProfileOutcome.RateLimited);
        writer.UpdateCalls.Should().Be(0);
    }

    [Fact]
    public async Task TamperedRepositoryResponseIsRejected()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();
        writer.ForceTampered = true;

        var result = await Service(writer).UpdateAsync("token", Request("Nuevo nombre"), default);

        result.Outcome.Should().Be(UpdateOwnSellerProfileOutcome.Unavailable);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task CriticalFailureIsUnavailable()
    {
        var writer = new InMemorySellerWriter { Fail = true };
        var result = await Service(writer).UpdateAsync("token", Request("Nuevo nombre"), default);

        result.Outcome.Should().Be(UpdateOwnSellerProfileOutcome.Unavailable);
    }

    private static UpdateSellerProfileRequest Request(string businessName) =>
        new(true, businessName, false, null, false, null);

    private static UpdateOwnSellerProfileService Service(InMemorySellerWriter writer,
        AuthenticatedProfileDouble? reader = null, SellerRateLimitDouble? limit = null) =>
        new(reader ?? new AuthenticatedProfileDouble
        {
            Profile = SellerFixtures.Buyer(withSellerRole: true, sellerId: SellerFixtures.SellerId)
        }, writer, limit ?? new SellerRateLimitDouble());
}
