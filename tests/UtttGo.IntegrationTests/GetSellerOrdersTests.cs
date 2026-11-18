using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

// Seller inbox: GET /api/v1/sellers/me/orders (D-ORD-05, spec.md v0.5 §5.10, T-ORD-21..23).
public sealed class GetSellerOrdersTests
{
    private const string Token = "test-token";

    [Fact]
    public async Task SellerReceivesOnlyOwnOrdersWithSellerRoleAndTotalCount()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order();
        orders.Orders["665f1d408b3f2a0012ab34cf"] = OrderFixtures.Order(
            buyerUserId: "other-buyer-id", sellerId: "665f1a108b3f2a0012ab34c2") with
        {
            OrderId = "665f1d408b3f2a0012ab34cf"
        };
        orders.Orders["665f1d408b3f2a0012ab34d0"] = OrderFixtures.Order(buyerUserId: "another-buyer-id") with
        {
            OrderId = "665f1d408b3f2a0012ab34d0"
        };
        using var factory = Factory(orders, profile: new AuthenticatedProfileDouble
        {
            Profile = OrderFixtures.SellerActor()
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync("/api/v1/sellers/me/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(["items", "page", "pageSize", "totalCount"]);
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(2);
        body.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        body.RootElement.GetProperty("pageSize").GetInt32().Should().Be(20);
        var items = body.RootElement.GetProperty("items");
        items.GetArrayLength().Should().Be(2);
        foreach (var item in items.EnumerateArray())
        {
            item.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
                ["orderId", "productId", "productName", "sellerId", "buyerId", "quantity", "unitPrice",
                    "totalAmount", "currency", "status", "role", "createdAt", "notes"]);
            item.GetProperty("sellerId").GetString().Should().Be(OrderFixtures.SellerId);
            item.GetProperty("role").GetString().Should().Be("seller");
            item.GetProperty("currency").GetString().Should().Be("MXN");
            item.GetProperty("totalAmount").GetDecimal().Should().Be(171.00m);
        }
        body.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("orderId").GetString())
            .Should().NotContain("665f1d408b3f2a0012ab34cf");
    }

    [Fact]
    public async Task SellerOrdersArePaginatedWithPageAndPageSize()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders["665f1d408b3f2a0012ab34d1"] = OrderFixtures.Order() with
        {
            OrderId = "665f1d408b3f2a0012ab34d1"
        };
        orders.Orders["665f1d408b3f2a0012ab34d2"] = OrderFixtures.Order() with
        {
            OrderId = "665f1d408b3f2a0012ab34d2"
        };
        orders.Orders["665f1d408b3f2a0012ab34d3"] = OrderFixtures.Order() with
        {
            OrderId = "665f1d408b3f2a0012ab34d3"
        };
        using var factory = Factory(orders, profile: new AuthenticatedProfileDouble
        {
            Profile = OrderFixtures.SellerActor()
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync("/api/v1/sellers/me/orders?page=2&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("page").GetInt32().Should().Be(2);
        body.RootElement.GetProperty("pageSize").GetInt32().Should().Be(2);
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(3);
        body.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task TheStatusFilterOnlyReturnsOrdersInThatState()
    {
        var orders = new OrdersRepositoryDouble();
        orders.Orders[OrderFixtures.OrderId] = OrderFixtures.Order(status: "Pending");
        orders.Orders["665f1d408b3f2a0012ab34d4"] = OrderFixtures.Order(status: "Accepted") with
        {
            OrderId = "665f1d408b3f2a0012ab34d4"
        };
        using var factory = Factory(orders, profile: new AuthenticatedProfileDouble
        {
            Profile = OrderFixtures.SellerActor()
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync("/api/v1/sellers/me/orders?status=Accepted");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
        body.RootElement.GetProperty("items")[0].GetProperty("status").GetString().Should().Be("Accepted");
    }

    [Fact]
    public async Task ANonSellerGetsSellerRoleRequired()
    {
        using var factory = Factory(profile: new AuthenticatedProfileDouble
        {
            Profile = SellerFixtures.Profile()
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync("/api/v1/sellers/me/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SELLER_ROLE_REQUIRED");
    }

    [Fact]
    public async Task AnInactiveSellerGetsSellerAccountNotActive()
    {
        using var factory = Factory(profile: new AuthenticatedProfileDouble
        {
            Profile = OrderFixtures.SellerActor(status: "Suspended")
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync("/api/v1/sellers/me/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SELLER_ACCOUNT_NOT_ACTIVE");
    }

    [Fact]
    public async Task WithoutSessionTheSellerOrdersAreUnauthorized()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/sellers/me/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED");
    }

    [Fact]
    public async Task ARateLimitedSellerGets429WithoutRunningTheQuery()
    {
        var orders = new OrdersRepositoryDouble();
        using var factory = Factory(orders, gate: new OrdersRateLimitDouble { Allowed = false },
            profile: new AuthenticatedProfileDouble { Profile = OrderFixtures.SellerActor() });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync("/api/v1/sellers/me/orders");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await response.Content.ReadAsStringAsync()).Should().Contain("RATE_LIMITED");
    }

    [Fact]
    public async Task AFailingRepositoryDoesNotLeakInternals()
    {
        var orders = new OrdersRepositoryDouble { Fail = true };
        using var factory = Factory(orders, profile: new AuthenticatedProfileDouble
        {
            Profile = OrderFixtures.SellerActor()
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.GetAsync("/api/v1/sellers/me/orders");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().Contain("ORDER_UNAVAILABLE");
        payload.Should().NotContain("internal-secret");
    }

    private static WebApplicationFactory<Program> Factory(OrdersRepositoryDouble? orders = null,
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
            services.AddSingleton<ICatalogOrderProducts>(new CatalogOrderProductsDouble
            {
                Product = OrderFixtures.Product()
            });
            services.AddSingleton<ISellerOrderProfiles>(new SellerOrderProfilesDouble
            {
                Profile = OrderFixtures.Seller()
            });
            services.AddSingleton<IAuthenticatedLocalProfileReader>(profile ?? new AuthenticatedProfileDouble
            {
                Profile = OrderFixtures.SellerActor()
            });
        }));
}
