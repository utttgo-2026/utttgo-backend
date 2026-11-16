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
public sealed class OrderJourneyTests : IAsyncLifetime
{
    private EndToEndHost host = null!;

    public async Task InitializeAsync()
    {
        host = await EndToEndHost.StartAsync();
        await host.Services.GetRequiredService<ICatalogSeeder>().SeedAsync(CancellationToken.None);
    }

    public async Task DisposeAsync() => await host.DisposeAsync();

    [Fact]
    public async Task BuyerBuysAProductAndBothParticipantsReadTheOrder()
    {
        var (sellerToken, categoryId) = await CreateSellerProductAsync();
        var buyerToken = await RegisterAndLoginAsync("comprador.e2e@uttt.edu.mx");

        var product = await GetProductAsync(sellerToken, categoryId);
        var productId = product.GetProperty("id").GetString()!;
        var price = product.GetProperty("price").GetDecimal();

        using var buyerClient = host.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var created = await buyerClient.PostAsJsonAsync("/api/v1/orders",
            new { productId, quantity = 2 });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var orderId = createdDocument.RootElement.GetProperty("orderId").GetString()!;
        createdDocument.RootElement.GetProperty("unitPrice").GetDecimal().Should().Be(price);
        createdDocument.RootElement.GetProperty("total").GetDecimal().Should().Be(price * 2);
        createdDocument.RootElement.GetProperty("status").GetString().Should().Be("Pending");
        createdDocument.RootElement.GetProperty("paymentMethod").GetString().Should().Be("Cash");
        createdDocument.RootElement.GetProperty("currency").GetString().Should().Be("MXN");
        createdDocument.RootElement.GetProperty("quantity").GetInt32().Should().Be(2);
        createdDocument.RootElement.GetProperty("pickupPoint").GetString().Should().Be("Biblioteca central");

        using var mine = await buyerClient.GetAsync("/api/v1/orders/mine");
        mine.StatusCode.Should().Be(HttpStatusCode.OK);
        using var mineDocument = JsonDocument.Parse(await mine.Content.ReadAsStringAsync());
        mineDocument.RootElement.GetProperty("total").GetInt64().Should().Be(1);
        mineDocument.RootElement.GetProperty("items")[0].GetProperty("orderId").GetString()
            .Should().Be(orderId);
        mineDocument.RootElement.GetProperty("items")[0].GetProperty("role").GetString().Should().Be("buyer");

        using var detail = await buyerClient.GetAsync($"/api/v1/orders/{orderId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);

        using var sellerClient = host.CreateClient();
        sellerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sellerToken);
        using var sellerView = await sellerClient.GetAsync($"/api/v1/orders/{orderId}");
        sellerView.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await host.Database.GetCollection<BsonDocument>("orders")
            .Find(Builders<BsonDocument>.Filter.Eq("productName", product.GetProperty("name").GetString()))
            .FirstAsync();
        stored["total"].BsonType.Should().Be(BsonType.Decimal128);
        stored["status"].AsString.Should().Be("Pending");
        stored["quantity"].AsInt32.Should().Be(2);

        var audits = await host.Database.GetCollection<BsonDocument>("orderAuditLogs")
            .Find(Builders<BsonDocument>.Filter.Eq("orderId", orderId)).ToListAsync();
        audits.Should().HaveCount(1);
        audits[0]["action"].AsString.Should().Be("Created");
        audits[0]["actorRole"].AsString.Should().Be("buyer");
    }

    [Fact]
    public async Task PurchaseReservesTheStockAndRejectsMoreThanAvailable()
    {
        var (sellerToken, categoryId) = await CreateSellerProductAsync();
        var buyerToken = await RegisterAndLoginAsync("comprador.stock@uttt.edu.mx");

        var product = await GetProductAsync(sellerToken, categoryId);
        var productId = product.GetProperty("id").GetString()!;
        var stock = product.GetProperty("stock").GetInt32();

        using var buyerClient = host.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        using var created = await buyerClient.PostAsJsonAsync("/api/v1/orders",
            new { productId, quantity = 2 });

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        using var sellerClient = host.CreateClient();
        sellerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sellerToken);
        using var afterPurchase = await sellerClient.GetAsync($"/api/v1/products/{productId}");
        afterPurchase.StatusCode.Should().Be(HttpStatusCode.OK);
        using var afterDocument = JsonDocument.Parse(await afterPurchase.Content.ReadAsStringAsync());
        afterDocument.RootElement.GetProperty("stock").GetInt32().Should().Be(stock - 2);

        using var tooMany = await buyerClient.PostAsJsonAsync("/api/v1/orders",
            new { productId, quantity = stock + 5 });

        tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooMany.Content.ReadAsStringAsync()).Should().Contain("INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task ThirdPartyDoesNotSeeTheOrder()
    {
        var (sellerToken, categoryId) = await CreateSellerProductAsync();
        var buyerToken = await RegisterAndLoginAsync("comprador.privado@uttt.edu.mx");
        var strangerToken = await RegisterAndLoginAsync("extraño.e2e@uttt.edu.mx");

        var product = await GetProductAsync(sellerToken, categoryId);
        var productId = product.GetProperty("id").GetString()!;

        using var buyerClient = host.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        using var created = await buyerClient.PostAsJsonAsync("/api/v1/orders", new { productId, quantity = 1 });
        using var createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var orderId = createdDocument.RootElement.GetProperty("orderId").GetString()!;

        using var strangerClient = host.CreateClient();
        strangerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", strangerToken);
        using var response = await strangerClient.GetAsync($"/api/v1/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task ConcurrentPurchasesWithTheSameIdempotencyKeyCreateOneOrderAndReserveTheStockOnce()
    {
        var (sellerToken, categoryId) = await CreateSellerProductAsync();
        var buyerToken = await RegisterAndLoginAsync("comprador.idempotente@uttt.edu.mx");

        var product = await GetProductAsync(sellerToken, categoryId);
        var productId = product.GetProperty("id").GetString()!;
        var stock = product.GetProperty("stock").GetInt32();
        var price = product.GetProperty("price").GetDecimal();
        var idempotencyKey = "idem-" + Guid.NewGuid().ToString("N");

        using var firstClient = host.CreateClient();
        using var secondClient = host.CreateClient();
        firstClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);
        secondClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerToken);

        var responses = await Task.WhenAll(
            PostOrderAsync(firstClient, productId, 2, idempotencyKey),
            PostOrderAsync(secondClient, productId, 2, idempotencyKey));

        responses.Select(response => response.Status).Should()
            .BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.OK]);
        var orderIds = responses.Select(response =>
            JsonDocument.Parse(response.Body).RootElement.GetProperty("orderId").GetString()).Distinct().ToList();
        orderIds.Should().HaveCount(1);

        var orders = await host.Database.GetCollection<BsonDocument>("orders")
            .Find(Builders<BsonDocument>.Filter.Eq("idempotencyKey", idempotencyKey)).ToListAsync();
        orders.Should().HaveCount(1);
        orders[0]["version"].AsInt64.Should().Be(0);
        orders[0]["total"].AsDecimal128.Should().Be(new Decimal128(price * 2));

        var storedProduct = await host.Database.GetCollection<BsonDocument>("products")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(productId))).FirstAsync();
        storedProduct["stock"].AsInt32.Should().Be(stock - 2);
        storedProduct["stockReservations"].AsBsonArray.Should().HaveCount(1);

        var audits = await host.Database.GetCollection<BsonDocument>("orderAuditLogs")
            .Find(Builders<BsonDocument>.Filter.Eq("orderId", orderIds[0])).ToListAsync();
        audits.Should().HaveCount(1);
    }

    [Fact]
    public async Task ConcurrentPurchasesNeverOversellTheStock()
    {
        var (sellerToken, categoryId) = await CreateSellerProductAsync(stock: 1);
        var firstBuyerToken = await RegisterAndLoginAsync("comprador.carrera.a@uttt.edu.mx");
        var secondBuyerToken = await RegisterAndLoginAsync("comprador.carrera.b@uttt.edu.mx");

        var product = await GetProductAsync(sellerToken, categoryId);
        var productId = product.GetProperty("id").GetString()!;

        using var firstClient = host.CreateClient();
        using var secondClient = host.CreateClient();
        firstClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstBuyerToken);
        secondClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondBuyerToken);

        var responses = await Task.WhenAll(
            PostOrderAsync(firstClient, productId, 1),
            PostOrderAsync(secondClient, productId, 1));

        responses.Select(response => response.Status).Should()
            .BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.BadRequest]);
        responses.Single(response => response.Status == HttpStatusCode.BadRequest).Body
            .Should().Contain("INSUFFICIENT_STOCK");

        var storedProduct = await host.Database.GetCollection<BsonDocument>("products")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(productId))).FirstAsync();
        storedProduct["stock"].AsInt32.Should().Be(0);

        var orders = await host.Database.GetCollection<BsonDocument>("orders")
            .Find(Builders<BsonDocument>.Filter.Eq("productId", productId)).ToListAsync();
        orders.Should().HaveCount(1);
    }

    private async Task<(string Token, string CategoryId)> CreateSellerProductAsync(int stock = 5)
    {
        var sellerToken = await RegisterAndLoginAsync("vendedor.pedidos@uttt.edu.mx");
        using var sellerClient = host.CreateClient();
        sellerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sellerToken);

        using var activation = await sellerClient.PostAsJsonAsync("/api/v1/sellers/activation-requests", new
        {
            businessName = "Puesto de Pedidos E2E",
            description = "Vendedor de prueba para pedidos",
            pickupPoint = "Biblioteca central"
        });
        activation.StatusCode.Should().Be(HttpStatusCode.Created);

        using var categories = await sellerClient.GetAsync("/api/v1/categories");
        using var categoriesDocument = JsonDocument.Parse(await categories.Content.ReadAsStringAsync());
        var categoryId = categoriesDocument.RootElement.GetProperty("items")[0].GetProperty("id").GetString()!;

        using var product = await sellerClient.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Tacos de pastor E2E " + Guid.NewGuid().ToString("N"),
            description = "Tacos con piña",
            ingredients = new[] { "Piña" },
            categoryId,
            price = 55.50m,
            stock
        });
        product.StatusCode.Should().Be(HttpStatusCode.Created);

        return (sellerToken, categoryId);
    }

    private async Task<JsonElement> GetProductAsync(string sellerToken, string categoryId)
    {
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);
        using var response = await client.GetAsync($"/api/v1/products?categoryId={categoryId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("items")[0].Clone();
    }

    private static async Task<(HttpStatusCode Status, string Body)> PostOrderAsync(HttpClient client,
        string productId, int quantity, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new { productId, quantity })
        };
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<string> RegisterAndLoginAsync(string email)
    {
        using var client = host.CreateClient();
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "E2E-only-password",
            displayName = "Usuario E2E"
        });
        registration.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "E2E-only-password"
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }
}
