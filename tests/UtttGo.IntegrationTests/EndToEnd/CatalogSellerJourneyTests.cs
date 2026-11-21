using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Catalog.Infrastructure;
using Xunit;

namespace UtttGo.IntegrationTests.EndToEnd;

[Trait("Category", "E2E")]
public sealed class CatalogSellerJourneyTests : IAsyncLifetime
{
    private const string Password = "E2E-only-password";
    private EndToEndHost host = null!;

    public async Task InitializeAsync()
    {
        host = await EndToEndHost.StartWithCatalogSeedAsync();
    }

    public async Task DisposeAsync() => await host.DisposeAsync();

    [Fact]
    public async Task SellerJourneyFromRegistrationToCatalogPublication()
    {
        const string email = "vendedor.e2e@uttt.edu.mx";

        using var registration = await host.Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = Password,
            displayName = "Vendedor E2E"
        });

        registration.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var registrationBody = await registration.Content.ReadAsStringAsync();
        registrationBody.Should().Contain("message").And.NotContain("accessToken")
            .And.NotContain(email).And.NotContain(Password);

        using var login = await host.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = Password
        });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        using var loginDocument = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        loginDocument.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["accessToken", "expiresIn", "tokenType", "user"]);
        var token = loginDocument.RootElement.GetProperty("accessToken").GetString()!;
        var userId = loginDocument.RootElement.GetProperty("user").GetProperty("id").GetString()!;
        userId.Should().MatchRegex("^[0-9a-fA-F]{24}$");
        loginDocument.RootElement.GetProperty("expiresIn").GetInt64().Should().Be(3600);
        loginDocument.RootElement.GetProperty("tokenType").GetString().Should().Be("Bearer");
        loginDocument.RootElement.GetProperty("user").GetProperty("accountStatus").GetString()
            .Should().Be("Active");
        Roles(loginDocument.RootElement.GetProperty("user").GetProperty("roles"))
            .Should().BeEquivalentTo(["Buyer"]);

        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var profile = await host.Client.GetAsync("/api/v1/users/me");

        profile.StatusCode.Should().Be(HttpStatusCode.OK);
        using var profileDocument = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());
        profileDocument.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["id", "displayName", "email", "phone", "avatarUrl", "roles"]);
        profileDocument.RootElement.GetProperty("id").GetString().Should().Be(userId);
        profileDocument.RootElement.GetProperty("email").GetString().Should().Be(email);
        profileDocument.RootElement.GetProperty("displayName").GetString().Should().Be("Vendedor E2E");
        Roles(profileDocument.RootElement.GetProperty("roles")).Should().BeEquivalentTo(["Buyer"]);

        using var activation = await host.Client.PostAsJsonAsync("/api/v1/sellers/activation-requests", new
        {
            storeName = "Puesto E2E",
            description = "Vendedor de prueba E2E",
            phone = "Biblioteca central"
        });

        activation.StatusCode.Should().Be(HttpStatusCode.Created);
        using var activationDocument = JsonDocument.Parse(await activation.Content.ReadAsStringAsync());
        activationDocument.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["sellerId", "storeName", "description", "phone", "status",
                "isActive", "roles", "createdAt", "updatedAt"]);
        var sellerId = activationDocument.RootElement.GetProperty("sellerId").GetString()!;
        sellerId.Should().MatchRegex("^[0-9a-fA-F]{24}$");
        activationDocument.RootElement.GetProperty("storeName").GetString().Should().Be("Puesto E2E");
        activationDocument.RootElement.GetProperty("status").GetString().Should().Be("Active");
        activationDocument.RootElement.GetProperty("isActive").GetBoolean().Should().BeTrue();
        Roles(activationDocument.RootElement.GetProperty("roles")).Should()
            .BeEquivalentTo(["Buyer", "Seller"]);

        using var categories = await host.Client.GetAsync("/api/v1/categories");

        categories.StatusCode.Should().Be(HttpStatusCode.OK);
        using var categoriesDocument = JsonDocument.Parse(await categories.Content.ReadAsStringAsync());
        categoriesDocument.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["items", "page", "pageSize", "totalCount"]);
        categoriesDocument.RootElement.GetProperty("totalCount").GetInt32().Should().Be(3);
        var categoryItems = categoriesDocument.RootElement.GetProperty("items").EnumerateArray().ToList();
        categoryItems.Select(item => item.GetProperty("name").GetString()).Should()
            .BeEquivalentTo(["Comida Preparada", "Bebidas", "Snacks"], options => options.WithStrictOrdering());
        categoryItems.Select(item => item.GetProperty("sortOrder").GetInt32()).Should()
            .BeEquivalentTo([10, 20, 30], options => options.WithStrictOrdering());
        var categoryId = categoryItems[0].GetProperty("id").GetString()!;
        categoryId.Should().MatchRegex("^[0-9a-fA-F]{24}$");

        using var created = await host.Client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Tacos de pastor E2E",
            description = "Tacos con piña",
            ingredients = new[] { "Piña", "Carne" },
            categoryId,
            price = 55.50m,
            stock = 10
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        createdDocument.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["id", "sellerId", "sellerDisplayName", "name", "description", "ingredients",
                "categoryId", "categoryName", "price", "stock", "status", "isAvailable", "ratingAverage",
                "ratingCount", "createdAt", "updatedAt", "imageUrl"]);
        var productId = createdDocument.RootElement.GetProperty("id").GetString()!;
        productId.Should().MatchRegex("^[0-9a-fA-F]{24}$");
        createdDocument.RootElement.GetProperty("sellerId").GetString().Should().Be(sellerId);
        createdDocument.RootElement.GetProperty("sellerDisplayName").GetString().Should().Be("Puesto E2E");
        createdDocument.RootElement.GetProperty("categoryId").GetString().Should().Be(categoryId);
        createdDocument.RootElement.GetProperty("categoryName").GetString().Should().Be("Comida Preparada");
        createdDocument.RootElement.GetProperty("price").GetDecimal().Should().Be(55.50m);
        createdDocument.RootElement.GetProperty("stock").GetInt32().Should().Be(10);
        createdDocument.RootElement.GetProperty("status").GetString().Should().Be("Active");
        createdDocument.RootElement.GetProperty("isAvailable").GetBoolean().Should().BeTrue();
        createdDocument.RootElement.GetProperty("ratingAverage").ValueKind.Should().Be(JsonValueKind.Null);
        createdDocument.RootElement.GetProperty("ratingCount").ValueKind.Should().Be(JsonValueKind.Null);

        var storedProduct = await host.Database.GetCollection<BsonDocument>("products")
            .Find(Builders<BsonDocument>.Filter.Eq("name", "Tacos de pastor E2E"))
            .FirstAsync();
        storedProduct["price"].BsonType.Should().Be(BsonType.Decimal128);
        storedProduct["price"].ToDecimal().Should().Be(55.50m);
        storedProduct["sellerId"].AsString.Should().Be(sellerId);
        storedProduct["categoryId"].AsString.Should().Be(categoryId);
        storedProduct["status"].AsString.Should().Be("Active");
        storedProduct["version"].AsInt64.Should().Be(0);

        using var listing = await host.Client.GetAsync("/api/v1/products");

        listing.StatusCode.Should().Be(HttpStatusCode.OK);
        using var listingDocument = JsonDocument.Parse(await listing.Content.ReadAsStringAsync());
        listingDocument.RootElement.EnumerateObject().Select(property => property.Name).Should()
            .BeEquivalentTo(["items", "page", "pageSize", "totalCount"]);
        var listedIds = listingDocument.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()).ToList();
        listedIds.Should().Contain(productId);

        using var detail = await host.Client.GetAsync($"/api/v1/products/{productId}");

        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        using var detailDocument = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        detailDocument.RootElement.GetProperty("id").GetString().Should().Be(productId);
        detailDocument.RootElement.GetProperty("sellerId").GetString().Should().Be(sellerId);
        detailDocument.RootElement.GetProperty("price").GetDecimal().Should().Be(55.50m);
        detailDocument.RootElement.GetProperty("stock").GetInt32().Should().Be(10);
        detailDocument.RootElement.GetProperty("categoryName").GetString().Should().Be("Comida Preparada");

        using var update = await host.Client.PatchAsync($"/api/v1/products/{productId}",
            JsonContent.Create(new { price = 60.00m, stock = 8 }));

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        using var updateDocument = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        updateDocument.RootElement.GetProperty("price").GetDecimal().Should().Be(60.00m);
        updateDocument.RootElement.GetProperty("stock").GetInt32().Should().Be(8);
        updateDocument.RootElement.GetProperty("status").GetString().Should().Be("Active");
        updateDocument.RootElement.GetProperty("isAvailable").GetBoolean().Should().BeTrue();

        using var availability = await host.Client.PatchAsync(
            $"/api/v1/products/{productId}/availability",
            JsonContent.Create(new { status = "Paused" }));

        availability.StatusCode.Should().Be(HttpStatusCode.OK);
        using var availabilityDocument = JsonDocument.Parse(await availability.Content.ReadAsStringAsync());
        availabilityDocument.RootElement.GetProperty("status").GetString().Should().Be("Paused");
        availabilityDocument.RootElement.GetProperty("stock").GetInt32().Should().Be(8);
        availabilityDocument.RootElement.GetProperty("isAvailable").GetBoolean().Should().BeFalse();

        using var pausedDetail = await host.Client.GetAsync($"/api/v1/products/{productId}");

        pausedDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await pausedDetail.Content.ReadAsStringAsync()).Should().Contain("PRODUCT_NOT_FOUND");

        using var pausedListing = await host.Client.GetAsync("/api/v1/products");

        pausedListing.StatusCode.Should().Be(HttpStatusCode.OK);
        using var pausedListingDocument = JsonDocument.Parse(await pausedListing.Content.ReadAsStringAsync());
        var pausedIds = pausedListingDocument.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()).ToList();
        pausedIds.Should().NotContain(productId);
    }

    [Fact]
    public async Task SeededCategoriesAreActiveAndIdempotentInMongo()
    {
        var seeder = host.Services.GetRequiredService<ICatalogSeeder>();

        var firstRun = await seeder.SeedAsync(CancellationToken.None);
        var secondRun = await seeder.SeedAsync(CancellationToken.None);

        firstRun.Should().HaveCount(3).And.OnlyContain(id => id.Length == 24);
        secondRun.Should().BeEquivalentTo(firstRun);

        var categories = host.Database.GetCollection<BsonDocument>("categories");
        var documents = await categories.Find(Builders<BsonDocument>.Filter.Eq("isActive", true)).ToListAsync();
        documents.Should().HaveCount(3);
        documents.Select(document => document["name"].AsString).Should()
            .BeEquivalentTo(["Comida Preparada", "Bebidas", "Snacks"]);
        documents.Select(document => document["normalizedName"].AsString).Should()
            .BeEquivalentTo(["comida preparada", "bebidas", "snacks"]);
        documents.Select(document => document["sortOrder"].AsInt32).Should()
            .BeEquivalentTo([10, 20, 30]);
    }

    [Fact]
    public async Task MongoIndexesOfTheJourneyAreCreated()
    {
        var indexes = await host.Database.GetCollection<BsonDocument>("users").Indexes.ListAsync();
        var userIndexNames = (await indexes.ToListAsync()).Select(index => index["name"].AsString).ToList();
        userIndexNames.Should().Contain("supabaseUserId_unique");

        indexes = await host.Database.GetCollection<BsonDocument>("sellers").Indexes.ListAsync();
        var sellerIndexNames = (await indexes.ToListAsync()).Select(index => index["name"].AsString).ToList();
        sellerIndexNames.Should().Contain("ownerUserId_unique");

        indexes = await host.Database.GetCollection<BsonDocument>("products").Indexes.ListAsync();
        var productIndexNames = (await indexes.ToListAsync()).Select(index => index["name"].AsString).ToList();
        productIndexNames.Should().Contain("price_status").And.Contain("sellerId_status");

        indexes = await host.Database.GetCollection<BsonDocument>("categories").Indexes.ListAsync();
        var categoryIndexNames = (await indexes.ToListAsync()).Select(index => index["name"].AsString).ToList();
        categoryIndexNames.Should().Contain("normalizedName_unique").And.Contain("isActive_sortOrder_name");
    }

    private static List<string?> Roles(JsonElement roles) =>
        roles.EnumerateArray().Select(role => role.GetString()).ToList();
}
