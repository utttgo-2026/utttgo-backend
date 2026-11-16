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

    [Theory]
    [InlineData("POST", "cancel")]
    [InlineData("PATCH", "status")]
    [InlineData("POST", "delivery-token")]
    [InlineData("POST", "delivery-confirmations")]
    public async Task TheOrderActionsWithoutSessionAreUnauthorizedAndNothingIsWritten(string method, string action)
    {
        var orders = new OrdersRepositoryDouble();
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(new HttpMethod(method),
            $"/api/v1/orders/{OrderFixtures.OrderId}/{action}")
        {
            Content = JsonContent.Create(new { })
        };
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED");
        orders.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task ANonParticipantDoesNotCancelTheOrderAndItsDataIsNotDisclosed()
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

        using var response = await client.PostAsync($"/api/v1/orders/{OrderFixtures.OrderId}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("ORDER_NOT_FOUND").And.NotContain("huitlacoche").And.NotContain("171");
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be("Pending");
    }

    [Fact]
    public async Task TheBuyerCannotMoveTheOrderToAccepted()
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
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be("Pending");
    }

    [Fact]
    public async Task TheSellerCannotIssueTheDeliveryToken()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order(status: "Ready");
        using var factory = Factory(orders: orders, profile: new AuthenticatedProfileDouble
        {
            Profile = OrderFixtures.SellerActor()
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsync($"/api/v1/orders/{OrderFixtures.OrderId}/delivery-token",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ORDER_FORBIDDEN");
        orders.Orders[OrderFixtures.OrderId].DeliveryTokenHash.Should().BeNull();
    }

    [Fact]
    public async Task TheBuyerCannotConfirmTheDelivery()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order(status: "Ready");
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/orders/{OrderFixtures.OrderId}/delivery-confirmations", new { token = "any-token-value" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ORDER_FORBIDDEN");
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be("Ready");
    }

    [Fact]
    public async Task TheDeliveryTokenIsNeverEchoedInTheOrderResponses()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order(status: "Ready");
        using var factory = Factory(orders: orders);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var tokenResponse = await client.PostAsync(
            $"/api/v1/orders/{OrderFixtures.OrderId}/delivery-token", null);
        using var tokenBody = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        var token = tokenBody.RootElement.GetProperty("token").GetString()!;
        using var detail = await client.GetAsync($"/api/v1/orders/{OrderFixtures.OrderId}");

        (await detail.Content.ReadAsStringAsync()).Should().NotContain(token);
        (await detail.Content.ReadAsStringAsync()).Should().NotContain("deliveryTokenHash");
    }

    [Fact]
    public async Task ARedisFailureFailsClosedOnTheOrderActions()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        using var factory = Factory(orders: orders, gate: new OrdersRateLimitDouble { Fail = true });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.PostAsync($"/api/v1/orders/{OrderFixtures.OrderId}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("ORDER_UNAVAILABLE").And.NotContain("internal-secret");
        orders.Orders[OrderFixtures.OrderId].Status.Should().Be("Pending");
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
