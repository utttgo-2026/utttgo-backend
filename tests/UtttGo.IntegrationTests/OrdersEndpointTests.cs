using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Modules.Sellers.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class OrdersEndpointTests
{
    private const string Token = "test-token";

    [Fact]
    public async Task CreatedOrderReturnsTheExactContractWithServerSideTotal()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync("/api/v1/orders", new { productId = OrderFixtures.ProductId, quantity = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            ["orderId", "productId", "productName", "sellerId", "buyerUserId", "quantity", "unitPrice",
                "total", "currency", "paymentMethod", "status", "pickupPoint", "deliveredAt", "completedAt",
                "createdAt", "updatedAt", "version", "notes"]);
        body.RootElement.GetProperty("unitPrice").GetDecimal().Should().Be(85.50m);
        body.RootElement.GetProperty("total").GetDecimal().Should().Be(171.00m);
        body.RootElement.GetProperty("quantity").GetInt32().Should().Be(2);
        body.RootElement.GetProperty("status").GetString().Should().Be("Pending");
        body.RootElement.GetProperty("paymentMethod").GetString().Should().Be("Cash");
        body.RootElement.GetProperty("currency").GetString().Should().Be("MXN");
        body.RootElement.GetProperty("sellerId").GetString().Should().Be(OrderFixtures.SellerId);
        body.RootElement.GetProperty("buyerUserId").GetString().Should().Be(SellerFixtures.UserId);
        body.RootElement.GetProperty("deliveredAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AnOrderCanCarryAnOptionalNoteForTheSeller()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync("/api/v1/orders",
            new { productId = OrderFixtures.ProductId, quantity = 1, notes = "Sin cebolla, por favor" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("notes").GetString().Should().Be("Sin cebolla, por favor");
    }

    [Fact]
    public async Task ANoteLongerThan250CharactersIsRejected()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync("/api/v1/orders",
            new { productId = OrderFixtures.ProductId, quantity = 1, notes = new string('a', 251) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
    }

    [Fact]
    public async Task CreatingAnOrderWithoutSessionIsUnauthorized()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/orders", new { productId = OrderFixtures.ProductId, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED");
    }

    [Theory]
    [InlineData("price")]
    [InlineData("total")]
    [InlineData("sellerId")]
    [InlineData("status")]
    [InlineData("paymentMethod")]
    [InlineData("currency")]
    public async Task PrivilegedFieldsFromTheClientAreRejected(string field)
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var content = new StringContent(
            $"{{\"productId\":\"{OrderFixtures.ProductId}\",\"quantity\":1,\"{field}\":\"1\"}}",
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/v1/orders", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
    }

    [Fact]
    public async Task InsufficientStockIsBadRequestAndNoOrderIsCreated()
    {
        var orders = new OrdersRepositoryDouble();
        using var factory = Factory(orders: orders, catalog: new CatalogOrderProductsDouble
        {
            Product = OrderFixtures.Product(stock: 1),
            ReserveOutcome = CatalogStockOutcome.InsufficientStock
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync("/api/v1/orders", new { productId = OrderFixtures.ProductId, quantity = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("INSUFFICIENT_STOCK");
        orders.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task UnknownProductIsNotFound()
    {
        using var factory = Factory(catalog: new CatalogOrderProductsDouble { Product = null });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync("/api/v1/orders", new { productId = OrderFixtures.ProductId, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task UnpublishedProductIsUnprocessable()
    {
        using var factory = Factory(catalog: new CatalogOrderProductsDouble
        {
            Product = OrderFixtures.Product(published: false)
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync("/api/v1/orders", new { productId = OrderFixtures.ProductId, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("PRODUCT_NOT_AVAILABLE");
    }

    [Fact]
    public async Task RateLimitedCreationReturns429WithoutCreatingTheOrder()
    {
        var orders = new OrdersRepositoryDouble();
        using var factory = Factory(orders: orders, gate: new OrdersRateLimitDouble { Allowed = false });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync("/api/v1/orders", new { productId = OrderFixtures.ProductId, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        orders.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task BuyerListsOnlyItsOwnOrders()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        orders.Orders["665f1d408b3f2a0012ab34cf"] = OrderFixtures.Order(buyerUserId: "other-buyer");
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync("/api/v1/orders/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["items", "page", "pageSize", "total"]);
        body.RootElement.GetProperty("total").GetInt64().Should().Be(1);
        body.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        body.RootElement.GetProperty("pageSize").GetInt32().Should().Be(20);
        var item = body.RootElement.GetProperty("items")[0];
        item.GetProperty("orderId").GetString().Should().Be(OrderFixtures.OrderId);
        item.GetProperty("role").GetString().Should().Be("buyer");
        item.GetProperty("total").GetDecimal().Should().Be(171.00m);
    }

    [Fact]
    public async Task ListingWithoutSessionIsUnauthorized()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/orders/mine");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvalidPaginationIsRejected()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync("/api/v1/orders/mine?page=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
    }

    [Fact]
    public async Task BuyerReadsTheOrderDetail()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync($"/api/v1/orders/{OrderFixtures.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("orderId").GetString().Should().Be(OrderFixtures.OrderId);
        body.RootElement.GetProperty("version").GetInt64().Should().Be(0);
    }

    [Fact]
    public async Task UnknownOrderIsNotFound()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync("/api/v1/orders/665f1d408b3f2a0012ab34ce");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task ThirdPartyDoesNotSeeTheOrder()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        using var factory = Factory(orders: orders, profile: new AuthenticatedProfileDouble
        {
            Profile = new LocalLoginProfile("other-user-id", "sub-2", "otro@uttt.edu.mx", "Active", ["Buyer"])
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync($"/api/v1/orders/{OrderFixtures.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ErrorsUseTheSingleErrorContract()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/orders/mine");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["correlationId", "status", "code", "message"]);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(401);
        body.RootElement.GetProperty("code").GetString().Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task TheBuyerCancelsItsPendingOrder()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsync($"/api/v1/orders/{OrderFixtures.OrderId}/cancel",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["orderId", "status", "completedAt", "version"]);
        body.RootElement.GetProperty("status").GetString().Should().Be("CancelledByBuyer");
        body.RootElement.GetProperty("version").GetInt64().Should().Be(1);
        body.RootElement.GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task RepeatingTheCancellationIsIdempotent()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var first = await client.PostAsync($"/api/v1/orders/{OrderFixtures.OrderId}/cancel", null);
        using var second = await client.PostAsync($"/api/v1/orders/{OrderFixtures.OrderId}/cancel", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be("CancelledByBuyer");
        body.RootElement.GetProperty("version").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task CancellingAnAcceptedOrderIsAConflict()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order(status: "Accepted");
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsync($"/api/v1/orders/{OrderFixtures.OrderId}/cancel",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ORDER_STATE_CONFLICT");
    }

    [Fact]
    public async Task TheSellerCannotCancelTheOrder()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        using var factory = Factory(orders: orders, profile: new AuthenticatedProfileDouble
        {
            Profile = OrderFixtures.SellerActor()
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsync($"/api/v1/orders/{OrderFixtures.OrderId}/cancel",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ORDER_FORBIDDEN");
    }

    [Fact]
    public async Task TheSellerMovesTheOrderToAccepted()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        using var factory = Factory(orders: orders, profile: new AuthenticatedProfileDouble
        {
            Profile = OrderFixtures.SellerActor()
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PatchAsJsonAsync($"/api/v1/orders/{OrderFixtures.OrderId}/status",
            new { status = "Accepted", version = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["orderId", "status", "version", "updatedAt"]);
        body.RootElement.GetProperty("status").GetString().Should().Be("Accepted");
        body.RootElement.GetProperty("version").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task TheBuyerCannotChangeTheStatus()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PatchAsJsonAsync($"/api/v1/orders/{OrderFixtures.OrderId}/status",
            new { status = "Accepted", version = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ORDER_FORBIDDEN");
    }

    [Fact]
    public async Task DeliveredIsRejectedInTheStatusRoute()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order(status: "Ready");
        using var factory = Factory(orders: orders, profile: new AuthenticatedProfileDouble
        {
            Profile = OrderFixtures.SellerActor()
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PatchAsJsonAsync($"/api/v1/orders/{OrderFixtures.OrderId}/status",
            new { status = "Delivered", version = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ORDER_STATE_CONFLICT");
    }

    [Fact]
    public async Task AStaleVersionIsAVersionConflict()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order(version: 2);
        using var factory = Factory(orders: orders, profile: new AuthenticatedProfileDouble
        {
            Profile = OrderFixtures.SellerActor()
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PatchAsJsonAsync($"/api/v1/orders/{OrderFixtures.OrderId}/status",
            new { status = "Accepted", version = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ORDER_VERSION_CONFLICT");
    }

    [Fact]
    public async Task TheStatusRequestRejectsPrivilegedFields()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PatchAsJsonAsync($"/api/v1/orders/{OrderFixtures.OrderId}/status",
            new { status = "Accepted", version = 0, total = 10, paymentMethod = "Card" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
    }

    [Fact]
    public async Task TheBuyerGetsADeliveryTokenWhenTheOrderIsReady()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order(status: "Ready");
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsync($"/api/v1/orders/{OrderFixtures.OrderId}/delivery-token",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["orderId", "token", "expiresAt"]);
        body.RootElement.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("expiresAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task TheTokenIsNotIssuedWhenTheOrderIsNotReady()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order(status: "InPreparation");
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsync($"/api/v1/orders/{OrderFixtures.OrderId}/delivery-token",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ORDER_STATE_CONFLICT");
    }

    [Fact]
    public async Task TheSellerConfirmsTheDeliveryWithTheToken()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order(status: "Ready");
        using var factory = Factory(orders: orders);
        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var tokenResponse = await buyerClient.PostAsync(
            $"/api/v1/orders/{OrderFixtures.OrderId}/delivery-token", null);
        using var tokenBody = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        var token = tokenBody.RootElement.GetProperty("token").GetString()!;

        using var sellerFactory = Factory(orders: orders, profile: new AuthenticatedProfileDouble
        {
            Profile = OrderFixtures.SellerActor()
        });
        using var seller = sellerFactory.CreateClient();
        seller.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await seller.PostAsJsonAsync(
            $"/api/v1/orders/{OrderFixtures.OrderId}/delivery-confirmations", new { token });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["orderId", "status", "deliveredAt", "version"]);
        body.RootElement.GetProperty("status").GetString().Should().Be("Delivered");
        body.RootElement.GetProperty("deliveredAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task AnInvalidDeliveryTokenIsRejected()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order(status: "Ready");
        using var factory = Factory(orders: orders, profile: new AuthenticatedProfileDouble
        {
            Profile = OrderFixtures.SellerActor()
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/orders/{OrderFixtures.OrderId}/delivery-confirmations", new { token = "not-the-token" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("DELIVERY_TOKEN_INVALID");
    }

    [Fact]
    public async Task TheOrderDetailNeverExposesTheDeliveryToken()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order(status: "Ready");
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var tokenResponse = await client.PostAsync(
            $"/api/v1/orders/{OrderFixtures.OrderId}/delivery-token", null);

        using var response = await client.GetAsync($"/api/v1/orders/{OrderFixtures.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().NotContain("deliveryToken");
        payload.Should().NotContain("token");
    }

    [Theory]
    [InlineData("POST", "cancel")]
    [InlineData("PATCH", "status")]
    [InlineData("POST", "delivery-token")]
    [InlineData("POST", "delivery-confirmations")]
    public async Task WithoutSessionTheOrderActionsAreUnauthorized(string method, string action)
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(new HttpMethod(method),
            $"/api/v1/orders/{OrderFixtures.OrderId}/{action}")
        {
            Content = JsonContent.Create(new { })
        };
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED");
    }

    private static WebApplicationFactory<Program> Factory(OrdersRepositoryDouble? orders = null,
        CatalogOrderProductsDouble? catalog = null, SellerOrderProfilesDouble? sellers = null,
        OrdersRateLimitDouble? gate = null, AuthenticatedProfileDouble? profile = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOrderRepository>();
            services.RemoveAll<IOrderAuditWriter>();
            services.RemoveAll<IOrdersRateLimitGate>();
            services.RemoveAll<ICatalogOrderProducts>();
            services.RemoveAll<ISellerOrderProfiles>();
            services.RemoveAll<IAuthenticatedLocalProfileReader>();
            services.AddSingleton<IOrderRepository>(orders ?? new OrdersRepositoryDouble());
            services.AddSingleton<IOrderAuditWriter>(new OrdersAuditWriterDouble());
            services.AddSingleton<IOrdersRateLimitGate>(gate ?? new OrdersRateLimitDouble());
            services.AddSingleton<ICatalogOrderProducts>(catalog ?? new CatalogOrderProductsDouble
            {
                Product = OrderFixtures.Product()
            });
            services.AddSingleton<ISellerOrderProfiles>(sellers ?? new SellerOrderProfilesDouble
            {
                Profile = OrderFixtures.Seller()
            });
            services.AddSingleton<IAuthenticatedLocalProfileReader>(profile ?? new AuthenticatedProfileDouble
            {
                Profile = SellerFixtures.Profile()
            });
        }));
}
