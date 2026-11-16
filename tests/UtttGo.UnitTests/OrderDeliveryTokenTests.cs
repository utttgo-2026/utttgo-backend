using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Orders.Contracts;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class OrderDeliveryTokenTests
{
    private static OrdersRepository RepositoryWith(OrderRecord order)
    {
        var repository = new OrdersRepository();
        repository.Orders[order.OrderId] = order;
        return repository;
    }

    private static CreateDeliveryTokenService TokenService(OrdersRepository orders, OrdersAuditWriter audit,
        OrdersRateLimitGate? gate = null, AuthenticatedProfileDouble? profile = null,
        int ttlSeconds = 900) =>
        new(profile ?? new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() },
            gate ?? new OrdersRateLimitGate(), orders,
            Options.Create(new OrderLimitsOptions { DeliveryTokenTtlSeconds = ttlSeconds }), audit,
            NullLogger<CreateDeliveryTokenService>.Instance);

    private static ConfirmDeliveryService ConfirmService(OrdersRepository orders, OrdersAuditWriter audit,
        CatalogOrderProducts catalog, OrdersRateLimitGate? gate = null,
        AuthenticatedProfileDouble? profile = null) =>
        new(profile ?? new AuthenticatedProfileDouble { Profile = OrderFixtures.SellerActor() },
            gate ?? new OrdersRateLimitGate(), orders, catalog, audit,
            NullLogger<ConfirmDeliveryService>.Instance);

    private static string ReadyOrderToken() => "delivery-token-value-1234567890";

    [Fact]
    public async Task TheBuyerGetsATokenWhenTheOrderIsReady()
    {
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Ready));
        var audit = new OrdersAuditWriter();

        var result = await TokenService(orders, audit).CreateAsync("token", OrderFixtures.OrderId,
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateDeliveryTokenOutcome.Created);
        result.Response!.Token.Should().NotBeNullOrWhiteSpace();
        result.Response.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        orders.Orders[OrderFixtures.OrderId].DeliveryTokenHash.Should()
            .Be(OrderDeliveryToken.Hash(result.Response.Token));
        orders.Orders[OrderFixtures.OrderId].DeliveryTokenHash.Should().NotContain(result.Response.Token);
        audit.Records.Should().ContainSingle(record => record.Action == "DeliveryTokenIssued");
    }

    [Fact]
    public async Task ANewTokenInvalidatesThePreviousOne()
    {
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Ready));
        var audit = new OrdersAuditWriter();
        var service = TokenService(orders, audit);

        var first = await service.CreateAsync("token", OrderFixtures.OrderId, "correlation",
            CancellationToken.None);
        var second = await service.CreateAsync("token", OrderFixtures.OrderId, "correlation",
            CancellationToken.None);

        first.Outcome.Should().Be(CreateDeliveryTokenOutcome.Created);
        second.Outcome.Should().Be(CreateDeliveryTokenOutcome.Created);
        var stored = orders.Orders[OrderFixtures.OrderId].DeliveryTokenHash;
        OrderDeliveryToken.Matches(first.Response!.Token, stored!).Should().BeFalse();
        OrderDeliveryToken.Matches(second.Response!.Token, stored!).Should().BeTrue();
    }

    [Fact]
    public async Task TheTokenCannotBeRequestedWhenTheOrderIsNotReady()
    {
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.InPreparation));
        var audit = new OrdersAuditWriter();

        var result = await TokenService(orders, audit).CreateAsync("token", OrderFixtures.OrderId,
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateDeliveryTokenOutcome.StateConflict);
        orders.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task TheSellerCannotRequestTheDeliveryToken()
    {
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Ready));
        var audit = new OrdersAuditWriter();
        var service = TokenService(orders, audit,
            profile: new AuthenticatedProfileDouble { Profile = OrderFixtures.SellerActor() });

        var result = await service.CreateAsync("token", OrderFixtures.OrderId, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(CreateDeliveryTokenOutcome.Forbidden);
        orders.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task RateLimitDenialDoesNotIssueAToken()
    {
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Ready));
        var audit = new OrdersAuditWriter();
        var gate = new OrdersRateLimitGate { Allowed = false };

        var result = await TokenService(orders, audit, gate).CreateAsync("token", OrderFixtures.OrderId,
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(CreateDeliveryTokenOutcome.RateLimited);
        orders.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task TheSellerConfirmsTheDeliveryWithTheToken()
    {
        var token = ReadyOrderToken();
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Ready,
            deliveryTokenHash: OrderDeliveryToken.Hash(token),
            deliveryTokenExpiresAt: DateTime.UtcNow.AddMinutes(15)));
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await ConfirmService(orders, audit, catalog).ConfirmAsync("token",
            OrderFixtures.OrderId, new DeliveryConfirmationRequest { Token = token }, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(ConfirmDeliveryOutcome.Delivered);
        result.Response!.Status.Should().Be(OrderStatus.Delivered);
        result.Response.DeliveredAt.Should().NotBeNull();
        result.Response.Version.Should().Be(1);
        orders.Orders[OrderFixtures.OrderId].CompletedAt.Should().NotBeNull();
        orders.Orders[OrderFixtures.OrderId].DeliveryTokenUsedAt.Should().NotBeNull();
        catalog.CommitCalls.Should().Be(1);
        audit.Records.Should().ContainSingle(record => record.Action == "DeliveryConfirmed" &&
            record.ActorRole == "seller" && record.ToStatus == OrderStatus.Delivered);
    }

    [Fact]
    public async Task AnInvalidTokenIsRejected()
    {
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Ready,
            deliveryTokenHash: OrderDeliveryToken.Hash(ReadyOrderToken()),
            deliveryTokenExpiresAt: DateTime.UtcNow.AddMinutes(15)));
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await ConfirmService(orders, audit, catalog).ConfirmAsync("token",
            OrderFixtures.OrderId, new DeliveryConfirmationRequest { Token = "another-token-value" },
            "correlation", CancellationToken.None);

        result.Outcome.Should().Be(ConfirmDeliveryOutcome.TokenInvalid);
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be(OrderStatus.Ready);
        catalog.CommitCalls.Should().Be(0);
    }

    [Fact]
    public async Task AMissingTokenIsInvalid()
    {
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Ready,
            deliveryTokenHash: OrderDeliveryToken.Hash(ReadyOrderToken()),
            deliveryTokenExpiresAt: DateTime.UtcNow.AddMinutes(15)));
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await ConfirmService(orders, audit, catalog).ConfirmAsync("token",
            OrderFixtures.OrderId, new DeliveryConfirmationRequest(), "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(ConfirmDeliveryOutcome.TokenInvalid);
    }

    [Fact]
    public async Task AnExpiredTokenIsRejected()
    {
        var token = ReadyOrderToken();
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Ready,
            deliveryTokenHash: OrderDeliveryToken.Hash(token),
            deliveryTokenExpiresAt: DateTime.UtcNow.AddMinutes(-1)));
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await ConfirmService(orders, audit, catalog).ConfirmAsync("token",
            OrderFixtures.OrderId, new DeliveryConfirmationRequest { Token = token }, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(ConfirmDeliveryOutcome.TokenExpired);
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be(OrderStatus.Ready);
    }

    [Fact]
    public async Task AnAlreadyUsedTokenIsRejected()
    {
        var token = ReadyOrderToken();
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Ready,
            deliveryTokenHash: OrderDeliveryToken.Hash(token),
            deliveryTokenExpiresAt: DateTime.UtcNow.AddMinutes(15),
            deliveryTokenUsedAt: DateTime.UtcNow.AddMinutes(-2)));
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await ConfirmService(orders, audit, catalog).ConfirmAsync("token",
            OrderFixtures.OrderId, new DeliveryConfirmationRequest { Token = token }, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(ConfirmDeliveryOutcome.TokenAlreadyUsed);
        catalog.CommitCalls.Should().Be(0);
    }

    [Fact]
    public async Task TheBuyerCannotConfirmTheDelivery()
    {
        var token = ReadyOrderToken();
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Ready,
            deliveryTokenHash: OrderDeliveryToken.Hash(token),
            deliveryTokenExpiresAt: DateTime.UtcNow.AddMinutes(15)));
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();
        var service = ConfirmService(orders, audit, catalog,
            profile: new AuthenticatedProfileDouble { Profile = SellerFixtures.Buyer() });

        var result = await service.ConfirmAsync("token", OrderFixtures.OrderId,
            new DeliveryConfirmationRequest { Token = token }, "correlation", CancellationToken.None);

        result.Outcome.Should().Be(ConfirmDeliveryOutcome.Forbidden);
        orders.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task ConfirmingAnOrderThatIsNotReadyIsAStateConflict()
    {
        var token = ReadyOrderToken();
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.InPreparation,
            deliveryTokenHash: OrderDeliveryToken.Hash(token),
            deliveryTokenExpiresAt: DateTime.UtcNow.AddMinutes(15)));
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        var result = await ConfirmService(orders, audit, catalog).ConfirmAsync("token",
            OrderFixtures.OrderId, new DeliveryConfirmationRequest { Token = token }, "correlation",
            CancellationToken.None);

        result.Outcome.Should().Be(ConfirmDeliveryOutcome.StateConflict);
        orders.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task TheTokenIsStoredOnlyAsAHash()
    {
        var token = ReadyOrderToken();
        var orders = RepositoryWith(OrderFixtures.Order(status: OrderStatus.Ready,
            deliveryTokenHash: OrderDeliveryToken.Hash(token),
            deliveryTokenExpiresAt: DateTime.UtcNow.AddMinutes(15)));
        var audit = new OrdersAuditWriter();
        var catalog = new CatalogOrderProducts();

        await ConfirmService(orders, audit, catalog).ConfirmAsync("token", OrderFixtures.OrderId,
            new DeliveryConfirmationRequest { Token = token }, "correlation", CancellationToken.None);

        orders.Orders[OrderFixtures.OrderId].DeliveryTokenHash.Should().NotContain(token);
        orders.Orders[OrderFixtures.OrderId].DeliveryTokenHash.Should()
            .Be(OrderDeliveryToken.Hash(token));
    }
}
