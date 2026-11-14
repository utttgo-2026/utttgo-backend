using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Sellers.Application;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class CatalogSecurityTests
{
    private const string Token = "private-test-token";

    [Fact]
    public async Task PublicCatalogIsReadableWithoutSessionAndNeverEchoesTheToken()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var anonymous = await client.GetAsync("/api/v1/products");
        anonymous.StatusCode.Should().Be(HttpStatusCode.OK);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var authenticated = await client.SendAsync(request);
        authenticated.StatusCode.Should().Be(HttpStatusCode.OK);

        var text = await authenticated.Content.ReadAsStringAsync();
        text.Should().Be(await anonymous.Content.ReadAsStringAsync());
        text.Should().NotContain(Token).And.NotContain("email").And.NotContain("phone")
            .And.NotContain("supabaseUserId").And.NotContain("ownerUserId").And.NotContain("version");
    }

    [Theory]
    [InlineData("POST", "/api/v1/products")]
    [InlineData("PATCH", "/api/v1/products/507f1f77bcf86cd799439021")]
    [InlineData("PATCH", "/api/v1/products/507f1f77bcf86cd799439021/availability")]
    public async Task WriteRoutesRequireASessionAndNeverWriteWithoutIt(string method, string url)
    {
        var products = new CatalogProductRepository();
        using var factory = Factory(products: products);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent("{\"price\":1}", Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED");
        products.CreateCalls.Should().Be(0);
        products.UpdateCalls.Should().Be(0);
    }

    [Fact]
    public async Task BuyerCannotPublishProducts()
    {
        using var factory = Factory(profile: SellerFixtures.Profile("Active"));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/products")
        {
            Content = new StringContent("""
                {"name":"Tacos","description":"Con piña","categoryId":"507f1f77bcf86cd799439031",
                 "price":55.50,"stock":10}
                """, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SELLER_ROLE_REQUIRED");
    }

    [Fact]
    public async Task ProductOfAnotherSellerIsNotEditableAndIsNotDisclosed()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product(CatalogFixtures.OtherSellerId);
        using var factory = Factory(products: products);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/products/{CatalogFixtures.ProductId}")
        {
            Content = new StringContent("{\"price\":1}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("Campus Snacks");
        products.UpdateCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("{\"sellerId\":\"other-seller\"}")]
    [InlineData("{\"status\":\"Active\"}")]
    [InlineData("{\"version\":9}")]
    [InlineData("{\"name\":null}")]
    [InlineData("{\"price\":0}")]
    [InlineData("{}")]
    public async Task PrivilegedOrInvalidFieldsAreRejectedWithoutWriting(string body)
    {
        var products = new CatalogProductRepository();
        using var factory = Factory(products: products);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/products/{CatalogFixtures.ProductId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        products.UpdateCalls.Should().Be(0);
    }

    [Fact]
    public async Task SearchTermIsTreatedLiterallyAndNeverLeaksAnError()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products?search=%2A%2A%2A");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("items").And.NotContain("internal-secret");
    }

    [Fact]
    public async Task RateLimitAndDependencyFailuresAreClosedAndQuiet()
    {
        using var limited = Factory(gate: new CatalogRateLimitGate { Allowed = false });
        using var limitedClient = limited.CreateClient();
        using var denied = await limitedClient.GetAsync("/api/v1/products");
        denied.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        using var failing = Factory(products: new CatalogProductRepository { Fail = true });
        using var failingClient = failing.CreateClient();
        using var failed = await failingClient.GetAsync("/api/v1/products");
        failed.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await failed.Content.ReadAsStringAsync()).Should().Contain("correlationId")
            .And.NotContain("internal-secret");
    }

    private static WebApplicationFactory<Program> Factory(CatalogProductRepository? products = null,
        CatalogCategoryRepository? categories = null, CatalogRateLimitGate? gate = null,
        LocalLoginProfile? profile = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICatalogRateLimitGate>();
            services.RemoveAll<IProductRepository>();
            services.RemoveAll<ICategoryRepository>();
            services.RemoveAll<IProductRatingsReader>();
            services.RemoveAll<ISellerDisplayNames>();
            services.RemoveAll<IAuthenticatedLocalProfileReader>();
            services.AddSingleton<ICatalogRateLimitGate>(gate ?? new CatalogRateLimitGate());
            services.AddSingleton<IProductRepository>(products ?? SeededProducts());
            services.AddSingleton<ICategoryRepository>(categories ?? SeededCategories());
            services.AddSingleton<IProductRatingsReader>(new CatalogRatingsReader());
            services.AddSingleton<ISellerDisplayNames>(SeededSellers());
            services.AddSingleton<IAuthenticatedLocalProfileReader>(new AuthenticatedProfileDouble
            {
                Profile = profile ?? SellerFixtures.Profile("Active", true, CatalogFixtures.SellerId)
            });
        }));

    private static CatalogProductRepository SeededProducts()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        return products;
    }

    private static CatalogCategoryRepository SeededCategories()
    {
        var categories = new CatalogCategoryRepository();
        categories.Categories[CatalogFixtures.CategoryId] = CatalogFixtures.Category();
        return categories;
    }

    private static CatalogSellerNames SeededSellers()
    {
        var sellers = new CatalogSellerNames();
        sellers.Names[CatalogFixtures.SellerId] = "Campus Snacks";
        return sellers;
    }
}
