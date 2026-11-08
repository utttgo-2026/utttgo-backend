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

namespace UtttGo.SecurityTests;

public sealed class SellerActivationSecurityTests
{
    private const string Path = "/api/v1/sellers/activation-requests";
    private const string ValidBody =
        "{\"businessName\":\"Campus Snacks\",\"description\":\"Snacks del campus\"," +
        "\"pickupPoint\":\"Biblioteca central\"}";

    [Theory]
    [InlineData("POST")]
    [InlineData("GET")]
    [InlineData("PATCH")]
    public async Task SellerRoutesRequireABearerToken(string method)
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), method == "POST" ? Path : "/api/v1/sellers/me")
        {
            Content = new StringContent(ValidBody, Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED")
            .And.Contain("correlationId");
    }

    [Theory]
    [InlineData("Basic test-jwt")]
    [InlineData("Bearer ")]
    [InlineData("Bearer test-jwt extra")]
    public async Task MalformedAuthorizationIsRejected(string header)
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

    [Theory]
    [InlineData("roles")]
    [InlineData("sellerId")]
    [InlineData("status")]
    [InlineData("version")]
    [InlineData("accountStatus")]
    [InlineData("email")]
    public async Task PrivilegedFieldsInTheBodyAreRejectedWithoutWriting(string field)
    {
        var writer = new InMemorySellerWriter();
        using var factory = Factory(writer: writer);
        var body = ValidBody.TrimEnd('}') + $",\"{field}\":\"injected\"}}";

        using var response = await Post(factory, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
        writer.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task QueryParametersCannotChangeTheActivatedIdentity()
    {
        var writer = new InMemorySellerWriter();
        var promotion = new SellerRolePromotionDouble();
        using var factory = Factory(writer: writer, promotion: promotion);

        using var response = await Post(factory, ValidBody,
            "/api/v1/sellers/activation-requests?ownerUserId=other-user&sellerId=507f1f77bcf86cd799439012");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        writer.LastOwnerUserId.Should().Be(SellerFixtures.UserId);
        promotion.LastUserId.Should().Be(SellerFixtures.UserId);
        writer.Sellers.Should().NotContainKey("other-user");
    }

    [Fact]
    public async Task ActivationResponseNeverLeaksAccountDataOrToken()
    {
        using var factory = Factory();
        using var response = await Post(factory, ValidBody);

        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("a@uttt.edu.mx").And.NotContain("1234567890").And.NotContain("supabaseUserId")
            .And.NotContain("test-jwt").And.NotContain("internal-secret");
    }

    [Fact]
    public async Task RateLimitExhaustionIs429AndDoesNotActivate()
    {
        var writer = new InMemorySellerWriter();
        var promotion = new SellerRolePromotionDouble();
        using var factory = Factory(writer: writer, promotion: promotion,
            limit: new SellerRateLimitDouble { Allowed = false });

        using var response = await Post(factory, ValidBody);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        writer.CreateCalls.Should().Be(0);
        promotion.Calls.Should().Be(0);
    }

    [Fact]
    public async Task RedisFailureFailsClosedWithoutActivating()
    {
        var writer = new InMemorySellerWriter();
        var promotion = new SellerRolePromotionDouble();
        using var factory = Factory(writer: writer, promotion: promotion,
            limit: new SellerRateLimitDouble { Fail = true });

        using var response = await Post(factory, ValidBody);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        writer.CreateCalls.Should().Be(0);
        promotion.Calls.Should().Be(0);
    }

    [Fact]
    public async Task InvalidSessionNeverActivatesAnySeller()
    {
        var writer = new InMemorySellerWriter();
        var promotion = new SellerRolePromotionDouble();
        using var factory = Factory(writer: writer, promotion: promotion,
            outcome: AuthenticatedLocalProfileOutcome.Unauthorized);

        using var response = await Post(factory, ValidBody);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        writer.CreateCalls.Should().Be(0);
        promotion.Calls.Should().Be(0);
    }

    private static async Task<HttpResponseMessage> Post(WebApplicationFactory<Program> factory, string body,
        string path = Path)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-jwt");
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.PostAsync(path, content);
    }

    internal static WebApplicationFactory<Program> Factory(InMemorySellerWriter? writer = null,
        SellerRolePromotionDouble? promotion = null, SellerRateLimitDouble? limit = null,
        AuthenticatedLocalProfileOutcome outcome = AuthenticatedLocalProfileOutcome.Success) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthenticatedLocalProfileReader>();
            services.RemoveAll<ISellerProfileWriter>();
            services.RemoveAll<ISellerRolePromotion>();
            services.RemoveAll<ISellerRateLimitGate>();
            services.AddSingleton<IAuthenticatedLocalProfileReader>(new AuthenticatedProfileDouble
            {
                Outcome = outcome,
                Profile = SellerFixtures.Profile()
            });
            services.AddSingleton<ISellerProfileWriter>(writer ?? new InMemorySellerWriter());
            services.AddSingleton<ISellerRolePromotion>(promotion ?? new SellerRolePromotionDouble());
            services.AddSingleton<ISellerRateLimitGate>(limit ?? new SellerRateLimitDouble());
        }));
}
