using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Application;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class UpdateOwnSellerProfileSecurityTests
{
    private const string Path = "/api/v1/sellers/me";

    [Theory]
    [InlineData("{\"businessName\":\"Nuevo nombre\"}")]
    [InlineData("{\"description\":\"Otra descripcion\"}")]
    [InlineData("{\"pickupPoint\":\"Otro punto\"}")]
    public async Task UpdateAlwaysTargetsTheAuthenticatedSeller(string body)
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();
        writer.Sellers["other-user"] = SellerFixtures.Seller("other-user", "507f1f77bcf86cd799439012");
        using var factory = Factory(writer: writer);

        using var response = await Patch(factory, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        writer.LastOwnerUserId.Should().Be(SellerFixtures.UserId);
        writer.Sellers["other-user"].BusinessName.Should().Be("Campus Snacks");
        writer.Sellers["other-user"].Version.Should().Be(1);
    }

    [Fact]
    public async Task UpdateCannotBeRedirectedWithQueryParameters()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();
        writer.Sellers["other-user"] = SellerFixtures.Seller("other-user", "507f1f77bcf86cd799439012");
        using var factory = Factory(writer: writer);

        using var response = await Patch(factory, "{\"businessName\":\"Nuevo nombre\"}",
            $"{Path}?ownerUserId=other-user&sellerId=507f1f77bcf86cd799439012");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        writer.LastOwnerUserId.Should().Be(SellerFixtures.UserId);
        writer.Sellers["other-user"].BusinessName.Should().Be("Campus Snacks");
    }

    [Theory]
    [InlineData("{\"roles\":[\"Admin\"]}")]
    [InlineData("{\"sellerId\":\"507f1f77bcf86cd799439012\"}")]
    [InlineData("{\"status\":\"Suspended\"}")]
    [InlineData("{\"version\":99}")]
    [InlineData("{\"ownerUserId\":\"other-user\"}")]
    public async Task PrivilegedFieldsAreRejectedWithoutWriting(string body)
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
    public async Task UpdateResponseNeverLeaksAccountData()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();
        using var factory = Factory(writer: writer);

        using var response = await Patch(factory, "{\"businessName\":\"Nuevo nombre\"}");

        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("a@uttt.edu.mx").And.NotContain("1234567890").And.NotContain("supabaseUserId")
            .And.NotContain("test-jwt");
    }

    [Fact]
    public async Task RedisFailureFailsClosedWithoutUpdating()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();
        using var factory = Factory(writer: writer, limit: new SellerRateLimitDouble { Fail = true });

        using var response = await Patch(factory, "{\"businessName\":\"Nuevo nombre\"}");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        writer.UpdateCalls.Should().Be(0);
        writer.Sellers[SellerFixtures.UserId].BusinessName.Should().Be("Campus Snacks");
    }

    [Fact]
    public async Task InvalidSessionNeverUpdates()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();
        using var factory = Factory(writer: writer,
            outcome: AuthenticatedLocalProfileOutcome.Unauthorized);

        using var response = await Patch(factory, "{\"businessName\":\"Nuevo nombre\"}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        writer.UpdateCalls.Should().Be(0);
        writer.Sellers[SellerFixtures.UserId].BusinessName.Should().Be("Campus Snacks");
    }

    [Fact]
    public async Task PublicSellerProfileNeverExposesPrivateBusinessData()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/sellers/{SellerFixtures.SellerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        using var body = System.Text.Json.JsonDocument.Parse(text);
        body.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            ["sellerId", "displayName", "ratingAverage", "ratingCount", "isActive"]);
        text.Should().NotContain("Biblioteca central").And.NotContain("Snacks del campus")
            .And.NotContain("ownerUserId").And.NotContain("local-id");
    }

    private static async Task<HttpResponseMessage> Patch(WebApplicationFactory<Program> factory, string body,
        string path = Path)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-jwt");
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.PatchAsync(path, content);
    }

    internal static WebApplicationFactory<Program> Factory(InMemorySellerWriter? writer = null,
        SellerRateLimitDouble? limit = null,
        AuthenticatedLocalProfileOutcome outcome = AuthenticatedLocalProfileOutcome.Success) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthenticatedLocalProfileReader>();
            services.RemoveAll<ISellerProfileWriter>();
            services.RemoveAll<ISellerProfileRepository>();
            services.RemoveAll<ISellerRolePromotion>();
            services.RemoveAll<ISellerRateLimitGate>();
            services.RemoveAll<IPublicSellerRateLimitGate>();
            services.RemoveAll<ISellerRatingsReader>();
            services.AddSingleton<IAuthenticatedLocalProfileReader>(new AuthenticatedProfileDouble
            {
                Outcome = outcome,
                Profile = SellerFixtures.Profile(withSellerRole: true, sellerId: SellerFixtures.SellerId)
            });
            services.AddSingleton<ISellerProfileWriter>(writer ?? new InMemorySellerWriter());
            services.AddSingleton<ISellerProfileRepository>(new PublicSellerProfileDouble(
                new SellerProfileSnapshot(SellerFixtures.SellerId, "Campus Snacks", "Active")));
            services.AddSingleton<ISellerRolePromotion>(new SellerRolePromotionDouble());
            services.AddSingleton<ISellerRateLimitGate>(limit ?? new SellerRateLimitDouble());
            services.AddSingleton<IPublicSellerRateLimitGate>(new AllowedPublicGate());
            services.AddSingleton<ISellerRatingsReader>(new NoRatings());
        }));
}
