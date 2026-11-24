using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Orders.Contracts;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class OrderTransitionServiceTests
{
    private static OrdersRepository RepositoryWith(OrderRecord order)
    {
        var repository = new OrdersRepository();
        repository.Orders[order.OrderId] = order;
        return repository;
    }

    private static CancelOrderService CancelService(OrdersRepository orders, OrdersAuditWriter audit,
        CatalogOrderProducts catalog, OrdersRateLimitGate? gate = null,
        AuthenticatedProfileDouble? profile = null) =>
        new(profile ?? new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() },
            gate ?? new OrdersRateLimitGate(), orders, catalog, audit,
            NullLogger<CancelOrderService>.Instance);

    private static UpdateOrderStatusService StatusService(OrdersRepository orders, OrdersAuditWriter audit,
        CatalogOrderProducts catalog, OrdersRateLimitGate? gate = null,
        AuthenticatedProfileDouble? profile = null) =>
        new(profile ?? new AuthenticatedProfileDouble { Profile = OrderFixtures.SellerActor() },
            gate ?? new OrdersRateLimitGate(), orders, catalog, audit,
            NullLogger<UpdateOrderStatusService>.Instance);

    [Fact]
    public async Task BuyerCancelsAPendingOrderAndReleasesTheStock()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await CancelService(orders, audit, catalog).CancelAsync("token",
            OrderFixtures.OrderId, "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CancelOrderOutcome.Cancelled);
        result.Response!.Status.Should().Be(OrderStatus.CancelledByBuyer);
        result.Response.CompletedAt.Should().NotBeNull();
        result.Response.Version.Should().Be(1);
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be(OrderStatus.CancelledByBuyer);
        catalog.ReleaseCalls.Should().Be(1);
        audit.Records.Should().ContainSingle(record => record.Action == "Cancelled" &&
            record.ActorRole == "buyer" && record.ToStatus == OrderStatus.CancelledByBuyer);
    }

    [Fact]
    public async Task RepeatingTheCancellationKeepsTheStateAndDoesNotAuditTwice()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();
        var service = CancelService(orders, audit, catalog);

        var first = await service.CancelAsync("token", OrderFixtures.OrderId, "correlation",
            CancellationToken.None);
        var second = await service.CancelAsync("token", OrderFixtures.OrderId, "correlation",
            CancellationToken.None);

        first.Outcome.Should().Be(CancelOrderOutcome.Cancelled);
        second.Outcome.Should().Be(CancelOrderOutcome.AlreadyCancelled);
        second.Response!.Version.Should().Be(1);
        orders.Orders[OrderFixtures.OrderId].Version.Should().Be(1);
        audit.Records.Should().ContainSingle();
    }

    [Fact]
    public async Task CancellingAnAcceptedOrderIsRejectedUntilTheOwnerApprovesIt()
    {
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Accepted));
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await CancelService(orders, audit, catalog).CancelAsync("token",
            OrderFixtures.OrderId, "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CancelOrderOutcome.StateConflict);
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be(OrderStatus.Accepted);
        catalog.ReleaseCalls.Should().Be(0);
    }

    [Fact]
    public async Task TheSellerCannotCancelTheOrder()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();
        var service = CancelService(orders, audit, catalog,
            profile: new AuthenticatedProfileDouble { Profile = OrderFixtures.SellerActor() });

        var result = await service.CancelAsync("token", OrderFixtures.OrderId, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(CancelOrderOutcome.Forbidden);
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task AThirdPartyDoesNotSeeTheOrder()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();
        var service = CancelService(orders, audit, catalog,
            profile: new AuthenticatedProfileDouble
            {
                Profile = new LocalLoginProfile("other-local-id", "sub-2", "otro@uttt.edu.mx", "Active",
                    ["Buyer"])
            });

        var result = await service.CancelAsync("token", OrderFixtures.OrderId, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(CancelOrderOutcome.NotFound);
    }

    [Fact]
    public async Task MissingSessionIsUnauthorized()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();
        var service = CancelService(orders, audit, catalog,
            profile: new AuthenticatedProfileDouble
            {
                Outcome = AuthenticatedLocalProfileOutcome.Unauthorized
            });

        var result = await service.CancelAsync("token", OrderFixtures.OrderId, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(CancelOrderOutcome.Unauthorized);
        orders.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task AnInactiveAccountCannotCancel()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();
        var service = CancelService(orders, audit, catalog,
            profile: new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer("Suspended") });

        var result = await service.CancelAsync("token", OrderFixtures.OrderId, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(CancelOrderOutcome.AccountNotActive);
        orders.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task RateLimitDenialDoesNotCancelTheOrder()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();
        var gate = new OrdersRateLimitGate { Allowed = false };

        var result = await CancelService(orders, audit, catalog, gate).CancelAsync("token",
            OrderFixtures.OrderId, "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CancelOrderOutcome.RateLimited);
        orders.ApplyCalls.Should().Be(0);
        catalog.ReleaseCalls.Should().Be(0);
    }

    [Fact]
    public async Task AStockReleaseFailureIsReportedWithoutHidingTheCancellation()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts { Fail = true };

        var result = await CancelService(orders, audit, catalog).CancelAsync("token",
            OrderFixtures.OrderId, "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CancelOrderOutcome.Unavailable);
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be(OrderStatus.CancelledByBuyer);
    }

    [Fact]
    public async Task TheSellerMovesAPendingOrderToAccepted()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await StatusService(orders, audit, catalog).UpdateAsync("token",
            OrderFixtures.OrderId, new UpdateOrderStatusRequest { Status = OrderStatus.Accepted, Version = 0 },
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(UpdateOrderStatusOutcome.Updated);
        result.Response!.Status.Should().Be(OrderStatus.Accepted);
        result.Response.Version.Should().Be(1);
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be(OrderStatus.Accepted);
        audit.Records.Should().ContainSingle(record => record.Action == "StatusChanged" &&
            record.ActorRole == "seller" && record.FromStatus == OrderStatus.Pending &&
            record.ToStatus == OrderStatus.Accepted);
    }

    [Fact]
    public async Task TheSellerWalksTheOrderUntilItIsReady()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();
        var service = StatusService(orders, audit, catalog);

        await service.UpdateAsync("token", OrderFixtures.OrderId,
            new UpdateOrderStatusRequest { Status = OrderStatus.Accepted, Version = 0 }, "correlation",
            CancellationToken.None);
        await service.UpdateAsync("token", OrderFixtures.OrderId,
            new UpdateOrderStatusRequest { Status = OrderStatus.InPreparation, Version = 1 }, "correlation",
            CancellationToken.None);
        var ready = await service.UpdateAsync("token", OrderFixtures.OrderId,
            new UpdateOrderStatusRequest { Status = OrderStatus.Ready, Version = 2 }, "correlation",
            CancellationToken.None);

        ready.Outcome.Should().Be(UpdateOrderStatusOutcome.Updated);
        ready.Response!.Status.Should().Be(OrderStatus.Ready);
        orders.Orders[OrderFixtures.OrderId].Version.Should().Be(3);
        audit.Records.Should().HaveCount(3);
    }

    [Fact]
    public async Task RejectingAnOrderReleasesTheStockAndCompletesIt()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await StatusService(orders, audit, catalog).UpdateAsync("token",
            OrderFixtures.OrderId, new UpdateOrderStatusRequest { Status = OrderStatus.Rejected, Version = 0 },
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(UpdateOrderStatusOutcome.Updated);
        result.Response!.Status.Should().Be(OrderStatus.Rejected);
        orders.Orders[OrderFixtures.OrderId].CompletedAt.Should().NotBeNull();
        catalog.ReleaseCalls.Should().Be(1);
    }

    [Fact]
    public async Task RejectedOrderWithStockReleaseFailureHasNoRetryThroughTheSameStatusRoute()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts { Fail = true };
        var service = StatusService(orders, audit, catalog);
        var request = new UpdateOrderStatusRequest { Status = OrderStatus.Rejected, Version = 0 };

        var first = await service.UpdateAsync("token", OrderFixtures.OrderId, request, "correlation",
            CancellationToken.None);

        first.Outcome.Should().Be(UpdateOrderStatusOutcome.Unavailable);
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be(OrderStatus.Rejected);
        orders.Orders[OrderFixtures.OrderId].Version.Should().Be(1);

        catalog.Fail = false;
        var retry = await service.UpdateAsync("token", OrderFixtures.OrderId,
            new UpdateOrderStatusRequest { Status = OrderStatus.Rejected, Version = 1 }, "correlation",
            CancellationToken.None);

        retry.Outcome.Should().Be(UpdateOrderStatusOutcome.StateConflict);
        catalog.ReleaseCalls.Should().Be(0);
    }

    [Fact]
    public async Task TheBuyerCannotChangeTheStatus()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();
        var service = StatusService(orders, audit, catalog,
            profile: new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() });

        var result = await service.UpdateAsync("token", OrderFixtures.OrderId,
            new UpdateOrderStatusRequest { Status = OrderStatus.Accepted, Version = 0 }, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(UpdateOrderStatusOutcome.Forbidden);
        orders.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task DeliveredCannotBeReachedThroughTheStatusRoute()
    {
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Ready));
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await StatusService(orders, audit, catalog).UpdateAsync("token",
            OrderFixtures.OrderId, new UpdateOrderStatusRequest { Status = OrderStatus.Delivered, Version = 0 },
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(UpdateOrderStatusOutcome.StateConflict);
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be(OrderStatus.Ready);
    }

    [Fact]
    public async Task AnInvalidTransitionIsRejected()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await StatusService(orders, audit, catalog).UpdateAsync("token",
            OrderFixtures.OrderId,
            new UpdateOrderStatusRequest { Status = OrderStatus.InPreparation, Version = 0 }, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(UpdateOrderStatusOutcome.StateConflict);
        orders.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task AStaleVersionIsAVersionConflict()
    {
        var orders = RepositoryWith(OrderFixtures.Order(version: 3));
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await StatusService(orders, audit, catalog).UpdateAsync("token",
            OrderFixtures.OrderId, new UpdateOrderStatusRequest { Status = OrderStatus.Accepted, Version = 1 },
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(UpdateOrderStatusOutcome.VersionConflict);
        orders.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task TheSellerDoesNotSeeAnUnknownOrder()
    {
        var orders = new OrdersRepository();
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await StatusService(orders, audit, catalog).UpdateAsync("token",
            OrderFixtures.UnknownOrderId,
            new UpdateOrderStatusRequest { Status = OrderStatus.Accepted, Version = 0 }, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(UpdateOrderStatusOutcome.NotFound);
    }

    [Fact]
    public async Task RateLimitDenialDoesNotChangeTheStatus()
    {
        var orders = RepositoryWith(OrderFixtures.Order());
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();
        var gate = new OrdersRateLimitGate { Allowed = false };

        var result = await StatusService(orders, audit, catalog, gate).UpdateAsync("token",
            OrderFixtures.OrderId, new UpdateOrderStatusRequest { Status = OrderStatus.Accepted, Version = 0 },
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(UpdateOrderStatusOutcome.RateLimited);
        orders.ApplyCalls.Should().Be(0);
    }
}
