using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Orders.Contracts;
using UTTTGO.Api.Modules.Sellers.Application;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class OrderCreationServiceTests
{
    private static CreateOrderService Service(OrdersRepository orders, CatalogOrderProducts catalog,
        SellerOrderProfiles sellers, OrdersRateLimitGate? gate = null, OrdersAuditWriter? audit = null,
        AuthenticatedProfileDouble? profile = null, NotificationPublisher? notifications = null,
        IAccountDeletionFence? deletionFence = null) =>
        new(profile ?? new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() },
            gate ?? new OrdersRateLimitGate(), catalog, sellers, orders,
            audit ?? new OrdersAuditWriter(), NullLogger<CreateOrderService>.Instance,
            deletionFence ?? new AccountDeletionFenceDouble(), notifications);

    private static CreateOrderRequest Request(int quantity = 2, string productId = OrderFixtures.ProductId,
        string? notes = null) =>
        new() { ProductId = productId, Quantity = quantity, Notes = notes };

    [Fact]
    public async Task AccountDeletionWinningTheFenceStopsAStaleBuyerOrder()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product() };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };
        var profile = new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() };
        var fence = new AccountDeletionFenceDouble(() => profile.Profile = SellerFixtures.Buyer("Deleted"));

        var result = await Service(orders, catalog, sellers, profile: profile, deletionFence: fence)
            .CreateAsync("token", Request(), null, "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.AccountNotActive);
        orders.CreateCalls.Should().Be(0);
        catalog.ReserveCalls.Should().Be(0);
    }

    [Fact]
    public async Task TotalIsCalculatedFromTheProductPriceAndQuantity()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product(price: 85.50m) };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };

        var result = await Service(orders, catalog, sellers).CreateAsync("token", Request(2), null,
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.Created);
        result.Response!.UnitPrice.Should().Be(85.50m);
        result.Response.TotalAmount.Should().Be(171.00m);
        result.Response.Quantity.Should().Be(2);
        result.Response.Currency.Should().Be("MXN");
        result.Response.PaymentMethod.Should().Be("Cash");
        result.Response.Status.Should().Be("Pending");
        result.Response.BuyerId.Should().Be(OrderFixtures.BuyerUserId);
        result.Response.SellerId.Should().Be(OrderFixtures.SellerId);
        result.Response.ProductName.Should().Be("Quesadillas de huitlacoche (6 pza)");
        result.Response.PickupPoint.Should().Be("Biblioteca central, entrada norte");
        orders.CreateCalls.Should().Be(1);
    }

    [Fact]
    public async Task StockIsReservedForTheRequestedQuantityBeforeTheOrderIsCreated()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product() };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };

        await Service(orders, catalog, sellers).CreateAsync("token", Request(3), null, "correlation",
            CancellationToken.None);

        catalog.ReserveCalls.Should().Be(1);
        catalog.LastQuantity.Should().Be(3);
        catalog.LastReservationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InsufficientStockIsRejectedAndNoOrderIsCreated()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts
        {
            Product = OrderFixtures.Product(stock: 1),
            ReserveOutcome = CatalogStockOutcome.InsufficientStock
        };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };

        var result = await Service(orders, catalog, sellers).CreateAsync("token", Request(2), null,
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.InsufficientStock);
        orders.CreateCalls.Should().Be(0);
        orders.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownProductIsNotFound()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = null };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };

        var result = await Service(orders, catalog, sellers).CreateAsync("token", Request(), null,
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.ProductNotFound);
        orders.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task UnpublishedProductIsNotAvailable()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product(published: false) };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };

        var result = await Service(orders, catalog, sellers).CreateAsync("token", Request(), null,
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.ProductNotAvailable);
        catalog.ReserveCalls.Should().Be(0);
    }

    [Fact]
    public async Task SellerThatIsNotActiveMakesTheProductUnavailable()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product() };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller(status: "Suspended") };

        var result = await Service(orders, catalog, sellers).CreateAsync("token", Request(), null,
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.ProductNotAvailable);
        catalog.ReserveCalls.Should().Be(0);
    }

    [Fact]
    public async Task MissingSessionIsUnauthorized()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product() };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };
        var profile = new AuthenticatedProfileDouble
        {
            Outcome = AuthenticatedLocalProfileOutcome.Unauthorized
        };

        var result = await Service(orders, catalog, sellers, profile: profile).CreateAsync("token",
            Request(), null, "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.Unauthorized);
        orders.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task InactiveAccountCannotCreateOrders()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product() };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };
        var profile = new AuthenticatedProfileDouble
        {
            Profile = SellerFixtures.Buyer(status: "Suspended")
        };

        var result = await Service(orders, catalog, sellers, profile: profile).CreateAsync("token",
            Request(), null, "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.AccountNotActive);
        orders.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task RateLimitDenialDoesNotCreateTheOrder()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product() };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };
        var gate = new OrdersRateLimitGate { Allowed = false };

        var result = await Service(orders, catalog, sellers, gate).CreateAsync("token", Request(), null,
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.RateLimited);
        orders.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task FailedInsertReleasesTheReservedStock()
    {
        var orders = new OrdersRepository { FailCreate = true };
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product() };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };

        var result = await Service(orders, catalog, sellers).CreateAsync("token", Request(), null,
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.Unavailable);
        catalog.ReleaseCalls.Should().Be(1);
    }

    [Fact]
    public async Task RepeatedIdempotencyKeyReturnsTheSameOrderWithoutReservingAgain()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product() };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };
        var service = Service(orders, catalog, sellers);

        var first = await service.CreateAsync("token", Request(), "key-1", "correlation",
            CancellationToken.None);
        var second = await service.CreateAsync("token", Request(), "key-1", "correlation",
            CancellationToken.None);

        first.Outcome.Should().Be(CreateOrderOutcome.Created);
        second.Outcome.Should().Be(CreateOrderOutcome.AlreadyCreated);
        second.Response!.OrderId.Should().Be(first.Response!.OrderId);
        orders.CreateCalls.Should().Be(1);
        catalog.ReserveCalls.Should().Be(1);
    }

    [Theory]
    [InlineData(3, OrderFixtures.ProductId, "nota distinta")]
    [InlineData(2, "665f1c2e8b3f2a0012ab34ce", null)]
    [InlineData(2, OrderFixtures.ProductId, "nota distinta")]
    public async Task ReusedIdempotencyKeyWithDifferentRelevantBodyIsRejected(int quantity, string productId,
        string? notes)
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product() };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };
        var service = Service(orders, catalog, sellers);

        await service.CreateAsync("token", Request(notes: "nota", quantity: 2), "key-1", "correlation",
            CancellationToken.None);
        var result = await service.CreateAsync("token", Request(quantity, productId, notes), "key-1",
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.IdempotencyKeyReused);
        result.Response.Should().BeNull();
        orders.CreateCalls.Should().Be(1);
        catalog.ReserveCalls.Should().Be(1);
    }

    [Fact]
    public async Task CreationIsAuditedWithTheActorAndTheCorrelationId()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product() };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };
        var audit = new OrdersAuditWriter();

        await Service(orders, catalog, sellers, audit: audit).CreateAsync("token", Request(), null,
            "correlation-1", CancellationToken.None);

        audit.Records.Should().HaveCount(1);
        audit.Records[0].Action.Should().Be("Created");
        audit.Records[0].ActorUserId.Should().Be(OrderFixtures.BuyerUserId);
        audit.Records[0].ActorRole.Should().Be("buyer");
        audit.Records[0].ToStatus.Should().Be("Pending");
        audit.Records[0].CorrelationId.Should().Be("correlation-1");
    }

    [Fact]
    public async Task CreationNotificationTargetsTheSellerOwnerUser()
    {
        var notifications = new NotificationPublisher();
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };

        await Service(new OrdersRepository(), new CatalogOrderProducts { Product = OrderFixtures.Product() },
            sellers, notifications: notifications).CreateAsync("token", Request(), null, "correlation",
            CancellationToken.None);

        notifications.Events.Should().ContainSingle()
            .Which.RecipientUserId.Should().Be(OrderFixtures.SellerUserId);
    }

    [Fact]
    public async Task MissingSellerOwnerDoesNotFailOrderCreation()
    {
        var notifications = new NotificationPublisher();
        var sellers = new SellerOrderProfiles
        {
            Profile = new SellerOrderProfile(OrderFixtures.SellerId, "Active", "pickup")
        };

        var result = await Service(new OrdersRepository(),
            new CatalogOrderProducts { Product = OrderFixtures.Product() }, sellers,
            notifications: notifications).CreateAsync("token", Request(), null, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.Created);
        notifications.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task DependencyFailureIsUnavailableWithoutCreatingTheOrder()
    {
        var orders = new OrdersRepository();
        var catalog = new CatalogOrderProducts { Product = OrderFixtures.Product(), Fail = true };
        var sellers = new SellerOrderProfiles { Profile = OrderFixtures.Seller() };

        var result = await Service(orders, catalog, sellers).CreateAsync("token", Request(), null,
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateOrderOutcome.Unavailable);
        orders.CreateCalls.Should().Be(0);
    }
}
