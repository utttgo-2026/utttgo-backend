using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Shared.Contracts;
using Xunit;

namespace UtttGo.IntegrationTests.EndToEnd;

// Journey: presigned intent -> confirmation -> product with imageUrl -> detail and list.
//
// CI never calls the real S3: S3MediaAssetService falls back to its mock implementation
// because no access key is configured, so the PUT upload step is skipped here. Uploading a
// real object to the bucket is a manual test; see docs/guia-integracion-movil.md.
[Trait("Category", "E2E")]
public sealed class ProductMediaJourneyTests : IAsyncLifetime
{
    private const string Password = "E2E-only-password";
    private EndToEndHost host = null!;
    private SellerSession owner = null!;

    public async Task InitializeAsync()
    {
        host = await EndToEndHost.StartWithCatalogSeedAsync();
        owner = await CreateSellerAsync("vendedor.foto.e2e@uttt.edu.mx", "Vendedor Foto E2E");
    }

    public async Task DisposeAsync() => await host.DisposeAsync();

    [Fact]
    public async Task SellerPublishesAPhotoConfirmedByMediaAndReadsItBack()
    {
        var categoryId = await FirstCategoryIdAsync();

        var asset = await PresignAsync(owner.Client);
        asset.UploadUrl.Should().StartWith("https://");

        // Manual step in a real environment: PUT the bytes to asset.UploadUrl before confirming.
        var imageUrl = await ConfirmAsync(owner.Client, asset.AssetId);

        using var created = await owner.Client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Tacos con foto E2E",
            description = "Tacos con piña",
            ingredients = new[] { "Piña", "Carne" },
            categoryId,
            price = 55.50m,
            stock = 10,
            imageUrl
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        createdDocument.RootElement.GetProperty("imageUrl").GetString().Should().Be(imageUrl);
        var productId = createdDocument.RootElement.GetProperty("id").GetString()!;

        using var detail = await owner.Client.GetAsync($"/api/v1/products/{productId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        using var detailDocument = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        detailDocument.RootElement.GetProperty("imageUrl").GetString().Should().Be(imageUrl);

        using var list = await owner.Client.GetAsync("/api/v1/products");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        using var listDocument = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var item = listDocument.RootElement.GetProperty("items").EnumerateArray()
            .Single(element => element.GetProperty("id").GetString() == productId);
        item.GetProperty("imageUrl").GetString().Should().Be(imageUrl);
    }

    [Fact]
    public async Task AnAssetThatWasNeverConfirmedIsRejected()
    {
        var categoryId = await FirstCategoryIdAsync();
        var asset = await PresignAsync(owner.Client);
        var publicUrl = $"{PublicBaseUrl.TrimEnd('/')}/{asset.ObjectKey}";

        using var response = await owner.Client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Tacos sin confirmar",
            description = "Tacos con piña",
            ingredients = new[] { "Piña" },
            categoryId,
            price = 40m,
            stock = 5,
            imageUrl = publicUrl
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
    }

    [Fact]
    public async Task AnAssetOwnedByAnotherSellerIsRejected()
    {
        var categoryId = await FirstCategoryIdAsync();
        var asset = await PresignAsync(owner.Client);
        var imageUrl = await ConfirmAsync(owner.Client, asset.AssetId);

        var intruder = await CreateSellerAsync("intruso.foto.e2e@uttt.edu.mx", "Intruso Foto E2E");
        using var response = await intruder.Client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Tacos ajenos",
            description = "Tacos con piña",
            ingredients = new[] { "Piña" },
            categoryId,
            price = 40m,
            stock = 5,
            imageUrl
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
    }

    [Fact]
    public async Task AnImageUrlFromAnotherHostIsRejected()
    {
        var categoryId = await FirstCategoryIdAsync();

        using var response = await owner.Client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Tacos de otro host",
            description = "Tacos con piña",
            ingredients = new[] { "Piña" },
            categoryId,
            price = 40m,
            stock = 5,
            imageUrl = "https://evil.example.com/staging/seller/photo"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("VALIDATION_ERROR");
    }

    private string PublicBaseUrl =>
        host.Services.GetRequiredService<IOptions<S3Config>>().Value.PublicBaseUrl;

    private async Task<string> FirstCategoryIdAsync()
    {
        using var categories = await owner.Client.GetAsync("/api/v1/categories");
        categories.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await categories.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("items")[0].GetProperty("id").GetString()!;
    }

    private async Task<PresignedAsset> PresignAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/media/presigned-url", new
        {
            fileName = "tacos-e2e",
            mimeType = "image/jpeg",
            size = 1024
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new PresignedAsset(
            document.RootElement.GetProperty("assetId").GetString()!,
            document.RootElement.GetProperty("uploadUrl").GetString()!,
            document.RootElement.GetProperty("objectKey").GetString()!);
    }

    private async Task<string> ConfirmAsync(HttpClient client, string assetId)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/media/confirm", new { assetId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("status").GetString().Should().Be("Ready");
        return document.RootElement.GetProperty("publicUrl").GetString()!;
    }

    private async Task<SellerSession> CreateSellerAsync(string email, string displayName)
    {
        using var registration = await host.Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = Password,
            displayName
        });
        registration.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using var login = await host.Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = Password
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        using var loginDocument = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = loginDocument.RootElement.GetProperty("accessToken").GetString()!;

        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var activation = await client.PostAsJsonAsync("/api/v1/sellers/activation-requests", new
        {
            storeName = displayName,
            description = "Vendedor de prueba E2E",
            phone = "Biblioteca central"
        });
        activation.StatusCode.Should().Be(HttpStatusCode.Created);

        return new SellerSession(client);
    }

    private sealed record SellerSession(HttpClient Client);

    private sealed record PresignedAsset(string AssetId, string UploadUrl, string ObjectKey);
}
