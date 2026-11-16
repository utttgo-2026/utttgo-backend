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
