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

namespace UtttGo.IntegrationTests;

public sealed class CatalogEndpointTests
{
    private const string Token = "test-token";

    [Fact]
    public async Task PublicListingReturnsPaginatedProductsWithoutPrivateData()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        body.RootElement.GetProperty("pageSize").GetInt32().Should().Be(20);
        body.RootElement.GetProperty("total").GetInt64().Should().Be(1);
        var item = body.RootElement.GetProperty("items")[0];
        item.GetProperty("id").GetString().Should().Be(CatalogFixtures.ProductId);
        item.GetProperty("sellerId").GetString().Should().Be(CatalogFixtures.SellerId);
        item.GetProperty("sellerDisplayName").GetString().Should().Be("Campus Snacks");
        item.GetProperty("categoryName").GetString().Should().Be("Comida");
        item.GetProperty("price").GetDecimal().Should().Be(55.50m);
        item.GetProperty("stock").GetInt32().Should().Be(10);
        item.GetProperty("isAvailable").GetBoolean().Should().BeTrue();
        item.GetProperty("ratingAverage").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("ratingCount").ValueKind.Should().Be(JsonValueKind.Null);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("email").And.NotContain("phone").And.NotContain("supabaseUserId");
    }

    [Theory]
    [InlineData("/api/v1/products?pageSize=999")]
    [InlineData("/api/v1/products?page=0")]
    [InlineData("/api/v1/products?sortBy=offers")]
    [InlineData("/api/v1/products?categoryId=not-an-id")]
    [InlineData("/api/v1/products?minPrice=abc")]
    public async Task InvalidQueryIsRejectedBeforeReadingTheCatalog(string url)
    {
        var products = new CatalogProductRepository();
        using var factory = Factory(products: products);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        products.ListCalls.Should().Be(0);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId");
    }

    [Fact]
    public async Task PublicDetailReturnsTheProductAndHidesUnknownOnes()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var found = await client.GetAsync($"/api/v1/products/{CatalogFixtures.ProductId}");
        found.StatusCode.Should().Be(HttpStatusCode.OK);

        using var missing = await client.GetAsync("/api/v1/products/507f1f77bcf86cd799439099");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await missing.Content.ReadAsStringAsync()).Should().Contain("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task MalformedProductIdIsRejectedBeforeTheRepository()
    {
        var products = new CatalogProductRepository();
        using var factory = Factory(products: products);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products/not-an-id");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("INVALID_PRODUCT_ID");
    }

    [Fact]
    public async Task PublicCategoriesAreListed()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items")[0].GetProperty("name").GetString().Should().Be("Comida");
    }

    [Fact]
    public async Task SellerPublishesItsOwnProduct()
    {
        var products = new CatalogProductRepository();
        using var factory = Factory(products: products);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/products")
        {
            Content = JsonContent("""
                {"name":"Tacos de pastor","description":"Tacos con piña","ingredients":["Piña"],
                 "categoryId":"507f1f77bcf86cd799439031","price":55.50,"stock":10}
                """)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("sellerId").GetString().Should().Be(CatalogFixtures.SellerId);
        body.RootElement.GetProperty("price").GetDecimal().Should().Be(55.50m);
        products.CreateCalls.Should().Be(1);
    }

    [Fact]
    public async Task PublishingWithoutSessionIsUnauthorized()
    {
        var products = new CatalogProductRepository();
        using var factory = Factory(products: products);
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/v1/products",
            JsonContent("""
                {"name":"Tacos","description":"Con piña","categoryId":"507f1f77bcf86cd799439031",
                 "price":55.50,"stock":10}
                """));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        products.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task PublishingWithInvalidBodyIsRejected()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/products")
        {
            Content = JsonContent("""
                {"name":"Tacos","description":"Con piña","categoryId":"507f1f77bcf86cd799439031",
                 "price":-5,"stock":10}
                """)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
    }

    [Fact]
    public async Task SellerEditsItsProductAndPausesItsPublication()
    {
        var products = new CatalogProductRepository();
        products.Products[CatalogFixtures.ProductId] = CatalogFixtures.Product();
        using var factory = Factory(products: products);
        using var client = factory.CreateClient();

        using var update = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/products/{CatalogFixtures.ProductId}")
        {
            Content = JsonContent("{\"price\":60.00}")
        };
        update.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var updated = await client.SendAsync(update);
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        using var availability = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/v1/products/{CatalogFixtures.ProductId}/availability")
        {
            Content = JsonContent("{\"availability\":\"Paused\"}")
        };
        availability.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var paused = await client.SendAsync(availability);
        paused.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await paused.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be("Paused");
        body.RootElement.GetProperty("stock").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task EditingWithoutSessionIsUnauthorized()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var response = await client.PatchAsync($"/api/v1/products/{CatalogFixtures.ProductId}",
            JsonContent("{\"price\":60.00}"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RateLimitDenialReturns429WithoutCatalogData()
    {
        var products = new CatalogProductRepository();
        using var factory = Factory(products: products, gate: new CatalogRateLimitGate { Allowed = false });
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products");
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        products.ListCalls.Should().Be(0);
    }

    [Fact]
    public async Task CriticalFailureReturns503WithoutExceptionDetails()
    {
        using var factory = Factory(products: new CatalogProductRepository { Fail = true });
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products");
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("correlationId")
            .And.NotContain("internal-secret");
    }

    [Fact]
    public async Task UnconfiguredApplicationFailsClosedForPublicReads()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/products");
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static StringContent JsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static WebApplicationFactory<Program> Factory(CatalogProductRepository? products = null,
        CatalogCategoryRepository? categories = null, CatalogRateLimitGate? gate = null,
        AuthenticatedLocalProfileOutcome outcome = AuthenticatedLocalProfileOutcome.Success) =>
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
                Outcome = outcome,
                Profile = CatalogFixtures.Seller()
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
