using FluentAssertions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Application;
using UTTTGO.Api.Modules.Sellers.Contracts;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class SellerActivationServiceTests
{
    [Fact]
    public async Task ActiveBuyerBecomesSellerWithServerGeneratedSellerId()
    {
        var writer = new InMemorySellerWriter();
        var promotion = new SellerRolePromotionDouble();
        var result = await Service(writer, promotion).ActivateAsync("token", Request(), default);

        result.Outcome.Should().Be(SellerActivationOutcome.Created);
        result.Response!.SellerId.Should().Be(SellerFixtures.SellerId);
        result.Response.BusinessName.Should().Be("Campus Snacks");
        result.Response.Status.Should().Be("Active");
        result.Response.IsActive.Should().BeTrue();
        result.Response.Roles.Should().Contain("Seller");
        writer.CreateCalls.Should().Be(1);
        promotion.Calls.Should().Be(1);
        promotion.LastUserId.Should().Be(SellerFixtures.UserId);
        promotion.LastSellerId.Should().Be(SellerFixtures.SellerId);
    }

    [Fact]
    public async Task RepeatedActivationReturnsSameSellerWithoutDuplicatingRole()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();
        var promotion = new SellerRolePromotionDouble();
        var reader = new AuthenticatedProfileDouble
        {
            Profile = SellerFixtures.Buyer(withSellerRole: true, sellerId: SellerFixtures.SellerId)
        };

        var result = await Service(writer, promotion, reader).ActivateAsync("token", Request(), default);

        result.Outcome.Should().Be(SellerActivationOutcome.AlreadyActive);
        result.Response!.SellerId.Should().Be(SellerFixtures.SellerId);
        writer.CreateCalls.Should().Be(0);
        promotion.Calls.Should().Be(0);
    }

    [Fact]
    public async Task PendingActivationIsCompletedOnRetry()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller(status: "PendingActivation", version: 0);
        var promotion = new SellerRolePromotionDouble();

        var result = await Service(writer, promotion).ActivateAsync("token", Request(), default);

        result.Outcome.Should().Be(SellerActivationOutcome.AlreadyActive);
        result.Response!.Status.Should().Be("Active");
        writer.ActivateCalls.Should().Be(1);
        writer.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task FailedRolePromotionKeepsSellerInvisibleAndFailsClosed()
    {
        var writer = new InMemorySellerWriter();
        var promotion = new SellerRolePromotionDouble { Outcome = SellerRolePromotionOutcome.Unavailable };

        var result = await Service(writer, promotion).ActivateAsync("token", Request(), default);

        result.Outcome.Should().Be(SellerActivationOutcome.Unavailable);
        writer.ActivateCalls.Should().Be(0);
        writer.Sellers[SellerFixtures.UserId].Status.Should().Be("PendingActivation");
    }

    [Fact]
    public async Task MissingLocalProfileDuringPromotionIsUnauthorized()
    {
        var promotion = new SellerRolePromotionDouble { Outcome = SellerRolePromotionOutcome.Missing };
        var result = await Service(new InMemorySellerWriter(), promotion).ActivateAsync("token", Request(), default);

        result.Outcome.Should().Be(SellerActivationOutcome.Unauthorized);
        promotion.Calls.Should().Be(1);
    }

    [Fact]
    public async Task NonActiveAccountIsForbiddenAndCreatesNothing()
    {
        var writer = new InMemorySellerWriter();
        var promotion = new SellerRolePromotionDouble();
        var reader = new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer(status: "PendingEmailVerification") };

        var result = await Service(writer, promotion, reader).ActivateAsync("token", Request(), default);

        result.Outcome.Should().Be(SellerActivationOutcome.AccountNotActive);
        writer.CreateCalls.Should().Be(0);
        promotion.Calls.Should().Be(0);
    }

    [Fact]
    public async Task MissingProfileIsReportedWithoutCreatingSeller()
    {
        var writer = new InMemorySellerWriter();
        var reader = new AuthenticatedProfileDouble
        {
            Outcome = AuthenticatedLocalProfileOutcome.ProfileMissing
        };

        var result = await Service(writer, new SellerRolePromotionDouble(), reader)
            .ActivateAsync("token", Request(), default);

        result.Outcome.Should().Be(SellerActivationOutcome.ProfileMissing);
        writer.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task UnauthorizedSessionNeverTouchesTheSellerCollection()
    {
        var writer = new InMemorySellerWriter();
        var reader = new AuthenticatedProfileDouble
        {
            Outcome = AuthenticatedLocalProfileOutcome.Unauthorized
        };

        var result = await Service(writer, new SellerRolePromotionDouble(), reader)
            .ActivateAsync("token", Request(), default);

        result.Outcome.Should().Be(SellerActivationOutcome.Unauthorized);
        writer.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task RateLimitDenialSkipsTheActivation()
    {
        var writer = new InMemorySellerWriter();
        var promotion = new SellerRolePromotionDouble();
        var limit = new SellerRateLimitDouble { Allowed = false };

        var result = await Service(writer, promotion, limit: limit).ActivateAsync("token", Request(), default);

        result.Outcome.Should().Be(SellerActivationOutcome.RateLimited);
        writer.CreateCalls.Should().Be(0);
        promotion.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("Suspended")]
    [InlineData("Deleted")]
    [InlineData("Inactive")]
    public async Task SellerInBlockedStateIsNotReactivated(string status)
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller(status: status);
        var promotion = new SellerRolePromotionDouble();

        var result = await Service(writer, promotion).ActivateAsync("token", Request(), default);

        result.Outcome.Should().Be(SellerActivationOutcome.Conflict);
        writer.ActivateCalls.Should().Be(0);
        promotion.Calls.Should().Be(0);
    }

    [Fact]
    public async Task CriticalDependencyFailureIsUnavailableWithoutLeakingDetails()
    {
        var writer = new InMemorySellerWriter { Fail = true };
        var result = await Service(writer, new SellerRolePromotionDouble())
            .ActivateAsync("token", Request(), default);

        result.Outcome.Should().Be(SellerActivationOutcome.Unavailable);
        result.Response.Should().BeNull();
    }

    private static SellerActivationRequest Request() => new()
    {
        BusinessName = "Campus Snacks",
        Description = "Snacks del campus",
        PickupPoint = "Biblioteca central"
    };

    private static SellerActivationService Service(InMemorySellerWriter writer,
        SellerRolePromotionDouble promotion, AuthenticatedProfileDouble? reader = null,
        SellerRateLimitDouble? limit = null) =>
        new(reader ?? new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() }, writer, promotion,
            limit ?? new SellerRateLimitDouble());
}
