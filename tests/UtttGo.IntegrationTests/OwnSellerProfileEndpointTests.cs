using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class OwnSellerProfileEndpointTests
{
    [Fact]
    public async Task OwnSellerProfileReturnsExactContract()
    {
        using var factory = Factory();
        using var response = await Get(factory);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        body.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            ["sellerId", "businessName", "description", "pickupPoint", "status", "isActive", "roles",
                "createdAt", "updatedAt"]);
        body.RootElement.GetProperty("sellerId").GetString().Should().Be(SellerFixtures.SellerId);
        text.Should().NotContain("a@uttt.edu.mx").And.NotContain("1234567890").And.NotContain("supabaseUserId")
            .And.NotContain("test-jwt");
    }

    [Fact]
    public async Task MeRouteIsNotThePublicSellerIdRoute()
    {
        using var factory = Factory();
        using var response = await Get(factory);

        (await response.Content.ReadAsStringAsync()).Should().NotContain("INVALID_SELLER_ID");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthenticatedUserWithoutSellerIs404()
    {
        using var factory = Factory(seller: false);

        using var response = await Get(factory);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SELLER_PROFILE_NOT_FOUND")
            .And.NotContain("Campus Snacks");
    }

    [Fact]
    public async Task MissingTokenIs401()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/sellers/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RateLimitDenialIs429()
    {
        using var factory = Factory(limit: new SellerRateLimitDouble { Allowed = false });

        using var response = await Get(factory);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await response.Content.ReadAsStringAsync()).Should().Contain("RATE_LIMITED");
    }

    [Fact]
    public async Task CriticalDependencyFailureIs503WithoutDetails()
    {
        using var factory = Factory(writer: new InMemorySellerWriter { Fail = true });

        using var response = await Get(factory);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SELLER_UNAVAILABLE")
            .And.NotContain("internal-secret");
    }

    internal static async Task<HttpResponseMessage> Get(WebApplicationFactory<Program> factory,
        string token = "test-jwt")
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync("/api/v1/sellers/me");
    }

    internal static WebApplicationFactory<Program> Factory(InMemorySellerWriter? writer = null, bool seller = true,
        SellerRateLimitDouble? limit = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var store = writer ?? new InMemorySellerWriter();
            if (seller) store.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();
            services.RemoveAll<IAuthenticatedLocalProfileReader>();
            services.RemoveAll<ISellerProfileWriter>();
            services.RemoveAll<ISellerRolePromotion>();
            services.RemoveAll<ISellerRateLimitGate>();
            services.AddSingleton<IAuthenticatedLocalProfileReader>(new AuthenticatedProfileDouble
            {
                Profile = SellerFixtures.Profile(withSellerRole: true, sellerId: SellerFixtures.SellerId)
            });
            services.AddSingleton<ISellerProfileWriter>(store);
            services.AddSingleton<ISellerRolePromotion>(new SellerRolePromotionDouble());
            services.AddSingleton<ISellerRateLimitGate>(limit ?? new SellerRateLimitDouble());
        }));
}
