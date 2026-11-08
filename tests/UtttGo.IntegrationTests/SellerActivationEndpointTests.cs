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

public sealed class SellerActivationEndpointTests
{
    private const string Path = "/api/v1/sellers/activation-requests";
    private const string ValidBody =
        "{\"businessName\":\"Campus Snacks\",\"description\":\"Snacks del campus\"," +
        "\"pickupPoint\":\"Biblioteca central\"}";

    [Fact]
    public async Task ActivationReturns201WithExactSellerContract()
    {
        using var factory = Factory();
        using var response = await Post(factory, ValidBody);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var text = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        body.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            ["sellerId", "businessName", "description", "pickupPoint", "status", "isActive", "roles",
                "createdAt", "updatedAt"]);
        body.RootElement.GetProperty("sellerId").GetString().Should().Be(SellerFixtures.SellerId);
        body.RootElement.GetProperty("businessName").GetString().Should().Be("Campus Snacks");
        body.RootElement.GetProperty("status").GetString().Should().Be("Active");
        body.RootElement.GetProperty("isActive").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("roles")[1].GetString().Should().Be("Seller");
        text.Should().NotContain("a@uttt.edu.mx").And.NotContain("phone").And.NotContain("supabaseUserId")
            .And.NotContain("test-jwt");
    }

    [Fact]
    public async Task RepeatedActivationReturns200WithTheSameSeller()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller();
        using var factory = Factory(writer: writer,
            profile: SellerFixtures.Profile(withSellerRole: true, sellerId: SellerFixtures.SellerId));

        using var response = await Post(factory, ValidBody);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("sellerId").GetString().Should().Be(SellerFixtures.SellerId);
        writer.CreateCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("{\"businessName\":\"Campus Snacks\",\"description\":\"Snacks\"}")]
    [InlineData("{\"businessName\":\"\",\"description\":\"Snacks\",\"pickupPoint\":\"Central\"}")]
    [InlineData("{\"businessName\":null,\"description\":\"Snacks\",\"pickupPoint\":\"Central\"}")]
    [InlineData("{\"businessName\":\"Campus\",\"description\":\"Snacks\",\"pickupPoint\":\"Central\",\"roles\":[\"Admin\"]}")]
    [InlineData("{\"businessName\":\"Campus\",\"description\":\"Snacks\",\"pickupPoint\":\"Central\",\"sellerId\":\"507f1f77bcf86cd799439011\"}")]
    [InlineData("{\"businessName\":\"Campus\",\"description\":\"Snacks\",\"pickupPoint\":\"Central\",\"status\":\"Active\"}")]
    [InlineData("{}")]
    public async Task InvalidBodyIs400AndWritesNothing(string body)
    {
        var writer = new InMemorySellerWriter();
        using var factory = Factory(writer: writer);

        using var response = await Post(factory, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR")
            .And.Contain("correlationId");
        writer.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task MissingTokenIs401()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var content = new StringContent(ValidBody, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(Path, content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED");
    }

    [Theory]
    [InlineData("Basic test-jwt")]
    [InlineData("Bearer ")]
    [InlineData("Bearer test-jwt extra")]
    public async Task MalformedAuthorizationIs401(string header)
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = new StringContent(ValidBody, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authorization", header).Should().BeTrue();

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NonActiveAccountIs403AndDoesNotCreateTheSeller()
    {
        var writer = new InMemorySellerWriter();
        using var factory = Factory(writer: writer, profile: SellerFixtures.Profile("PendingEmailVerification"));

        using var response = await Post(factory, ValidBody);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SELLER_ACCOUNT_NOT_ACTIVE");
        writer.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task MissingLocalProfileIs404()
    {
        using var factory = Factory(outcome: AuthenticatedLocalProfileOutcome.ProfileMissing);

        using var response = await Post(factory, ValidBody);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("PROFILE_NOT_FOUND");
    }

    [Fact]
    public async Task BlockedSellerStateIs409()
    {
        var writer = new InMemorySellerWriter();
        writer.Sellers[SellerFixtures.UserId] = SellerFixtures.Seller(status: "Suspended");
        using var factory = Factory(writer: writer);

        using var response = await Post(factory, ValidBody);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SELLER_STATE_CONFLICT");
    }

    [Fact]
    public async Task RateLimitDenialIs429()
    {
        using var factory = Factory(limit: new SellerRateLimitDouble { Allowed = false });

        using var response = await Post(factory, ValidBody);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await response.Content.ReadAsStringAsync()).Should().Contain("RATE_LIMITED");
    }

    [Fact]
    public async Task CriticalDependencyFailureIs503WithoutDetails()
    {
        using var factory = Factory(writer: new InMemorySellerWriter { Fail = true });

        using var response = await Post(factory, ValidBody);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SELLER_UNAVAILABLE")
            .And.Contain("correlationId").And.NotContain("internal-secret");
    }

    internal static async Task<HttpResponseMessage> Post(WebApplicationFactory<Program> factory, string body,
        string token = "test-jwt")
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.PostAsync(Path, content);
    }

    internal static WebApplicationFactory<Program> Factory(InMemorySellerWriter? writer = null,
        LocalLoginProfile? profile = null, AuthenticatedLocalProfileOutcome outcome =
            AuthenticatedLocalProfileOutcome.Success, SellerRateLimitDouble? limit = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthenticatedLocalProfileReader>();
            services.RemoveAll<ISellerProfileWriter>();
            services.RemoveAll<ISellerRolePromotion>();
            services.RemoveAll<ISellerRateLimitGate>();
            services.AddSingleton<IAuthenticatedLocalProfileReader>(new AuthenticatedProfileDouble
            {
                Outcome = outcome,
                Profile = profile ?? SellerFixtures.Profile()
            });
            services.AddSingleton<ISellerProfileWriter>(writer ?? new InMemorySellerWriter());
            services.AddSingleton<ISellerRolePromotion>(new SellerRolePromotionDouble());
            services.AddSingleton<ISellerRateLimitGate>(limit ?? new SellerRateLimitDouble());
        }));
}
