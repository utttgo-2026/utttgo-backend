using FluentAssertions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Orders.Contracts;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class OrderQueryServiceTests
{
    private static LocalLoginProfile SellerProfile() =>
        SellerFixtures.Buyer(withSellerRole: true, sellerId: OrderFixtures.SellerId);

    [Fact]
    public async Task BuyerSeesOnlyItsOwnOrders()
    {
        var orders = new OrdersRepository();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        orders.Orders["665f1d408b3f2a0012ab34cf"] = OrderFixtures.Order(buyerUserId: "other-buyer");
        var service = new ListOwnOrdersService(
            new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() },
            new OrdersRateLimitGate(), orders);

        var result = await service.ListAsync("token", new OrderListQuery(1, 20, null), CancellationToken.None);

        result.Outcome.Should().Be(ListOwnOrdersOutcome.Success);
        result.Response!.Total.Should().Be(1);
        result.Response.Items.Should().HaveCount(1);
        result.Response.Items[0].OrderId.Should().Be(OrderFixtures.OrderId);
        result.Response.Items[0].Role.Should().Be("buyer");
        result.Response.Items[0].Total.Should().Be(171.00m);
        result.Response.Items[0].Currency.Should().Be("MXN");
    }

    [Fact]
    public async Task ListingCanBeFilteredByStatus()
    {
        var orders = new OrdersRepository();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        orders.Orders["665f1d408b3f2a0012ab34cf"] = OrderFixtures.Order() with { Status = "Accepted" };
        var service = new ListOwnOrdersService(
            new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() },
            new OrdersRateLimitGate(), orders);

        var result = await service.ListAsync("token", new OrderListQuery(1, 20, "Accepted"),
            CancellationToken.None);

        result.Response!.Items.Should().HaveCount(1);
        result.Response.Items[0].Status.Should().Be("Accepted");
    }

    [Fact]
    public async Task ListingWithoutSessionIsUnauthorized()
    {
        var service = new ListOwnOrdersService(
            new AuthenticatedProfileDouble { Outcome = AuthenticatedLocalProfileOutcome.Unauthorized },
            new OrdersRateLimitGate(), new OrdersRepository());

        var result = await service.ListAsync("token", new OrderListQuery(1, 20, null), CancellationToken.None);

        result.Outcome.Should().Be(ListOwnOrdersOutcome.Unauthorized);
    }

    [Fact]
    public async Task RateLimitedListingDoesNotReadTheOrders()
    {
        var orders = new OrdersRepository();
        var service = new ListOwnOrdersService(
            new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() },
            new OrdersRateLimitGate { Allowed = false }, orders);

        var result = await service.ListAsync("token", new OrderListQuery(1, 20, null), CancellationToken.None);

        result.Outcome.Should().Be(ListOwnOrdersOutcome.RateLimited);
        orders.ListCalls.Should().Be(0);
    }

    [Fact]
    public async Task BuyerReadsItsOwnOrder()
    {
        var orders = new OrdersRepository();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        var service = new GetOrderService(new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() },
            new OrdersRateLimitGate(), orders);

        var result = await service.GetAsync("token", OrderFixtures.OrderId, CancellationToken.None);

        result.Outcome.Should().Be(GetOrderOutcome.Found);
        result.Response!.OrderId.Should().Be(OrderFixtures.OrderId);
        result.Response.BuyerUserId.Should().Be(OrderFixtures.BuyerUserId);
        result.Response.UnitPrice.Should().Be(85.50m);
        result.Response.Total.Should().Be(171.00m);
        result.Response.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task SellerOfTheOrderReadsIt()
    {
        var orders = new OrdersRepository();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        var service = new GetOrderService(
            new AuthenticatedProfileDouble { Profile = SellerProfile() }, new OrdersRateLimitGate(), orders);

        var result = await service.GetAsync("token", OrderFixtures.OrderId, CancellationToken.None);

        result.Outcome.Should().Be(GetOrderOutcome.Found);
    }

    [Fact]
    public async Task ThirdPartyDoesNotSeeTheOrder()
    {
        var orders = new OrdersRepository();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        var stranger = new LocalLoginProfile("other-user-id", "sub-2", "otro@uttt.edu.mx", "Active", ["Buyer"]);
        var service = new GetOrderService(
            new AuthenticatedProfileDouble { Profile = stranger }, new OrdersRateLimitGate(), orders);

        var result = await service.GetAsync("token", OrderFixtures.OrderId, CancellationToken.None);

        result.Outcome.Should().Be(GetOrderOutcome.NotFound);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task UnknownOrderIsNotFound()
    {
        var service = new GetOrderService(
            new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() },
            new OrdersRateLimitGate(), new OrdersRepository());

        var result = await service.GetAsync("token", OrderFixtures.UnknownOrderId, CancellationToken.None);

        result.Outcome.Should().Be(GetOrderOutcome.NotFound);
    }
}
