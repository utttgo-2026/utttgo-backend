using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

public sealed class UpdateOwnSellerProfileEndpointTests
{
    private const string Path = "/api/v1/sellers/me";

    [Fact]
    public async Task ValidUpdateReturns200WithUpdatedValues()
    {
        using var factory = Factory();

        using var response = await Patch(factory, "{\"businessName\":\"Nuevo nombre\"}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("businessName").GetString().Should().Be("Nuevo nombre");
        body.RootElement.GetProperty("description").GetString().Should().Be("Snacks del campus");
        body.RootElement.GetProperty("pickupPoint").GetString().Should().Be("Biblioteca central");
    }

    [Theory]
    [InlineData("{\"roles\":[\"Admin\"]}")]
    [InlineData("{\"sellerId\":\"507f1f77bcf86cd799439011\"}")]
    [InlineData("{\"status\":\"Active\"}")]
    [InlineData("{\"version\":7}")]
    [InlineData("{\"businessName\":null}")]
    [InlineData("{\"businessName\":\"\"}")]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task InvalidBodyIs400AndWritesNothing(string body)
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();
        using var factory = Factory(writer: writer);

        using var response = await Patch(factory, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
        writer.UpdateCalls.Should().Be(0);
    }

    [Fact]
    public async Task MissingTokenIs401()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{\"businessName\":\"Nuevo nombre\"}", Encoding.UTF8,
            "application/json");

        using var response = await client.PatchAsync(Path, content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UserWithoutSellerIs404()
    {
        using var factory = Factory(seller: false);

        using var response = await Patch(factory, "{\"businessName\":\"Nuevo nombre\"}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SELLER_PROFILE_NOT_FOUND");
    }

    [Fact]
    public async Task ConcurrentUpdateIs409()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();
        writer.ForceConflict = true;
        using var factory = Factory(writer: writer);

        using var response = await Patch(factory, "{\"businessName\":\"Nuevo nombre\"}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SELLER_CONFLICT");
    }

    [Fact]
    public async Task RateLimitDenialIs429()
    {
        using var factory = Factory(limit: new SellerRateLimitDouble { Allowed = false });

        using var response = await Patch(factory, "{\"businessName\":\"Nuevo nombre\"}");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task CriticalDependencyFailureIs503WithoutDetails()
    {
        using var factory = Factory(writer: new InMemorySellerWriter { Fail = true });

        using var response = await Patch(factory, "{\"businessName\":\"Nuevo nombre\"}");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SELLER_UNAVAILABLE")
            .And.NotContain("internal-secret");
    }

    internal static async Task<HttpResponseMessage> Patch(WebApplicationFactory<Program> factory, string body,
        string token = "test-jwt")
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.PatchAsync(Path, content);
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
