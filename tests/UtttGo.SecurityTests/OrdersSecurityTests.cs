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

namespace UtttGo.SecurityTests;

public sealed class OrdersSecurityTests
{
    private const string Token = "private-test-token";

    [Fact]
    public async Task CreatingAnOrderWithoutSessionIsUnauthorizedAndWritesNothing()
    {
        var orders = new OrdersRepositoryDouble();
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/orders",
            new { productId = OrderFixtures.ProductId, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED");
        orders.CreateCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("/api/v1/orders/mine")]
    [InlineData("/api/v1/orders/665f1d408b3f2a0012ab34ce")]
    public async Task ReadingOrdersWithoutSessionIsUnauthorized(string url)
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED");
    }

    [Fact]
    public async Task AnInvalidExpiredOrRevokedSessionIsRejectedAndWritesNothing()
    {
        var orders = new OrdersRepositoryDouble();
        using var factory = Factory(orders: orders, profile: new AuthenticatedProfileDouble
        {
            Outcome = AuthenticatedLocalProfileOutcome.Unauthorized
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync("/api/v1/orders",
            new { productId = OrderFixtures.ProductId, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED");
        orders.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task AnInactiveAccountCannotBuy()
    {
        var orders = new OrdersRepositoryDouble();
        using var factory = Factory(orders: orders, profile: new AuthenticatedProfileDouble
        {
            Profile = SellerFixtures.Profile("Pending")
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync("/api/v1/orders",
            new { productId = OrderFixtures.ProductId, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ACCOUNT_NOT_ACTIVE");
        orders.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task ANonParticipantDoesNotSeeTheOrderAndItsDataIsNotDisclosed()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        using var factory = Factory(orders: orders, profile: new AuthenticatedProfileDouble
        {
            Profile = new LocalLoginProfile(OrderFixtures.OtherBuyerId, "sub-2", "otro@uttt.edu.mx", "Active",
                ["Buyer"])
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync($"/api/v1/orders/{OrderFixtures.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("ORDER_NOT_FOUND").And.NotContain("huitlacoche").And.NotContain("171");
    }

    [Theory]
    [InlineData("price")]
    [InlineData("total")]
    [InlineData("sellerId")]
    [InlineData("buyerUserId")]
    [InlineData("status")]
    [InlineData("paymentMethod")]
    [InlineData("currency")]
    [InlineData("version")]
    [InlineData("reservationId")]
    [InlineData("roles")]
    [InlineData("role")]
    public async Task PrivilegedFieldsFromTheClientAreRejectedAndNothingIsWritten(string field)
    {
        var orders = new OrdersRepositoryDouble();
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var content = new StringContent(
            $"{{\"productId\":\"{OrderFixtures.ProductId}\",\"quantity\":1,\"{field}\":\"1\"}}",
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/v1/orders", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
        orders.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task ListingOwnOrdersNeverReturnsAnotherBuyersOrders()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        orders.Orders["665f1d408b3f2a0012ab34cf"] = OrderFixtures.Order(OrderFixtures.OtherBuyerId);
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync("/api/v1/orders/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("total").GetInt64().Should().Be(1);
        body.RootElement.GetProperty("items")[0].GetProperty("orderId").GetString()
            .Should().Be(OrderFixtures.OrderId);
    }

    [Fact]
    public async Task TheRateLimitIsClosedAndNothingIsWritten()
    {
        var orders = new OrdersRepositoryDouble();
        using var factory = Factory(orders: orders, gate: new OrdersRateLimitDouble { Allowed = false });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync("/api/v1/orders",
            new { productId = OrderFixtures.ProductId, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await response.Content.ReadAsStringAsync()).Should().Contain("RATE_LIMITED");
        orders.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task ARedisFailureFailsClosedAndDoesNotLeakTheInternalError()
    {
        var orders = new OrdersRepositoryDouble();
        using var factory = Factory(orders: orders, gate: new OrdersRateLimitDouble { Fail = true });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync("/api/v1/orders",
            new { productId = OrderFixtures.ProductId, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("ORDER_UNAVAILABLE").And.NotContain("internal-secret");
        orders.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task TheSessionTokenIsNeverEchoedInAnyOrderResponse()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var detail = await client.GetAsync($"/api/v1/orders/{OrderFixtures.OrderId}");
        using var mine = await client.GetAsync("/api/v1/orders/mine");

        (await detail.Content.ReadAsStringAsync()).Should().NotContain(Token);
        (await mine.Content.ReadAsStringAsync()).Should().NotContain(Token);
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
                Profile = SellerFixtures.Profile("Active")
            });
        }));
}
