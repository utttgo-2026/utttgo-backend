using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Media.Application;
using Xunit;

namespace UtttGo.IntegrationTests.EndToEnd;

[Trait("Category", "E2E")]
public sealed class MediaCatalogJourneyTests : IAsyncLifetime
{
    private const string Bucket = "utttgo-media-local-test";
    private const string ImageType = "image/jpeg";
    private IContainer localstack = null!;
    private AmazonS3Client s3 = null!;
    private EndToEndHost host = null!;

    public async Task InitializeAsync()
    {
        localstack = new ContainerBuilder("localstack/localstack:4.9")
            .WithEnvironment("SERVICES", "s3").WithPortBinding(4566, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                request => request.ForPort(4566).ForPath("/_localstack/health"))).Build();
        await localstack.StartAsync();
        s3 = new AmazonS3Client(new BasicAWSCredentials("local-test", "local-test"),
            new AmazonS3Config
            {
                ServiceURL = $"http://{localstack.Hostname}:{localstack.GetMappedPublicPort(4566)}",
                ForcePathStyle = true
            });
        await s3.PutBucketAsync(new PutBucketRequest { BucketName = Bucket });
        host = await EndToEndHost.StartWithCatalogSeedAsync(services =>
        {
            services.RemoveAll<IMediaObjectStore>();
            services.RemoveAll<IImageModerator>();
            services.AddSingleton<IMediaObjectStore>(new LocalS3Store(s3));
            // Approval is controlled only by this test; application default remains fail-closed.
            services.AddSingleton<IImageModerator>(new LocalModerator());
        });
    }

    public async Task DisposeAsync()
    {
        if (host is not null) await host.DisposeAsync();
        s3?.Dispose();
        if (localstack is not null) await localstack.DisposeAsync();
    }

    [Fact]
    public async Task UploadConfirmPublishReplaceRemoveAndPreserveThroughMongoRedisAndS3()
    {
        var client = host.Client;
        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "media.e2e@uttt.edu.mx",
            password = "Local-test-only-1!",
            displayName = "Media E2E"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "media.e2e@uttt.edu.mx",
            password = "Local-test-only-1!"
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            loginJson.RootElement.GetProperty("accessToken").GetString());
        using var activation = await client.PostAsJsonAsync("/api/v1/sellers/activation-requests",
            new { storeName = "Fotos E2E", description = "Prueba local", phone = "Campus" });
        activation.StatusCode.Should().Be(HttpStatusCode.Created);
        using var categories = await client.GetAsync("/api/v1/categories");
        using var categoriesJson = JsonDocument.Parse(await categories.Content.ReadAsStringAsync());
        var categoryId = categoriesJson.RootElement.GetProperty("items")[0].GetProperty("id").GetString();

        using var without = await client.PostAsJsonAsync("/api/v1/products",
            new { name = "Sin foto", description = "Sin foto", categoryId, price = 10m, stock = 2 });
        without.StatusCode.Should().Be(HttpStatusCode.Created);
        using var withoutJson = JsonDocument.Parse(await without.Content.ReadAsStringAsync());
        withoutJson.RootElement.GetProperty("imageUrl").ValueKind.Should().Be(JsonValueKind.Null);
        var productId = withoutJson.RootElement.GetProperty("id").GetString();

        var firstId = await UploadAndApprove(client, "one.jpg");
        var secondId = await UploadAndApprove(client, "two.jpg");
        using var withImage = await client.PostAsJsonAsync("/api/v1/products",
            new
            {
                name = "Con foto",
                description = "Foto aprobada",
                categoryId,
                price = 12m,
                stock = 1,
                mediaAssetId = firstId
            });
        withImage.StatusCode.Should().Be(HttpStatusCode.Created);
        using var withImageJson = JsonDocument.Parse(await withImage.Content.ReadAsStringAsync());
        await AssertPresignedGet(withImageJson.RootElement);
        await PatchImage(client, productId!, $$"""{"mediaAssetId":"{{firstId}}"}""", firstId);
        await PatchImage(client, productId!, """{"stock":3}""", firstId);
        await PatchImage(client, productId!, $$"""{"mediaAssetId":"{{secondId}}"}""", secondId);

        using var listing = await client.GetAsync("/api/v1/products");
        using var listingJson = JsonDocument.Parse(await listing.Content.ReadAsStringAsync());
        var image = listingJson.RootElement.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("id").GetString() == productId);
        await AssertPresignedGet(image);
        using var detail = await client.GetAsync($"/api/v1/products/{productId}");
        using var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        await AssertPresignedGet(detailJson.RootElement);

        var stored = await host.Database.GetCollection<BsonDocument>("products")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(productId)))
            .FirstAsync();
        stored["mediaAssetId"].AsString.Should().Be(secondId);
        await PatchImage(client, productId!, """{"mediaAssetId":null}""", null);
    }

    private async Task<string> UploadAndApprove(HttpClient client, string file)
    {
        using var intent = await client.PostAsJsonAsync("/api/v1/media/upload-url",
            new { fileName = file, mimeType = ImageType, size = 10 });
        intent.StatusCode.Should().Be(HttpStatusCode.Created);
        using var json = JsonDocument.Parse(await intent.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("mediaAssetId").GetString()!;
        var upload = json.RootElement.GetProperty("uploadUrl").GetString()!;
        json.RootElement.GetProperty("headers").GetProperty("Content-Type").GetString().Should().Be(ImageType);
        using var body = new ByteArrayContent(Encoding.ASCII.GetBytes("0123456789"));
        body.Headers.ContentType = new MediaTypeHeaderValue(ImageType);
        using var put = await new HttpClient().PutAsync(upload, body);
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        using var complete = await client.PostAsJsonAsync("/api/v1/media/confirm",
            new { mediaAssetId = id });
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        (await complete.Content.ReadAsStringAsync()).Should().Contain(MediaStatus.Approved);
        using var repeat = await client.PostAsJsonAsync($"/api/v1/media/{id}/complete", new { });
        (await repeat.Content.ReadAsStringAsync()).Should().Contain(MediaStatus.Approved);
        return id;
    }

    private static async Task PatchImage(HttpClient client, string id, string json, string? expected)
    {
        using var patch = await client.PatchAsync($"/api/v1/products/{id}",
            new StringContent(json, Encoding.UTF8, "application/json"));
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        using var response = JsonDocument.Parse(await patch.Content.ReadAsStringAsync());
        if (expected is null)
            response.RootElement.GetProperty("imageUrl").ValueKind.Should().Be(JsonValueKind.Null);
        else
            await AssertPresignedGet(response.RootElement);
    }

    private static async Task AssertPresignedGet(JsonElement product)
    {
        var url = product.GetProperty("imageUrl").GetString();
        url.Should().Contain("X-Amz-Signature").And.Contain(Bucket);
        product.GetProperty("imageUrlExpiresAt").GetDateTime().Should().BeAfter(DateTime.UtcNow);
        using var response = await new HttpClient().GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Should().HaveCount(10);
    }

    private sealed class LocalModerator : IImageModerator
    {
        public Task<bool> IsSafeAsync(string objectKey, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class LocalS3Store(AmazonS3Client client) : IMediaObjectStore
    {
        public Task<(string Url, DateTime ExpiresAt, IReadOnlyDictionary<string, string> Headers)>
            SignPutAsync(string key, string mime, CancellationToken ct)
        {
            var expires = DateTime.UtcNow.AddMinutes(4);
            var url = client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = Bucket,
                Key = key,
                Verb = HttpVerb.PUT,
                ContentType = mime,
                Expires = expires,
                Protocol = Protocol.HTTP
            });
            return Task.FromResult<(string, DateTime, IReadOnlyDictionary<string, string>)>(
                (url, expires, new Dictionary<string, string> { ["Content-Type"] = mime }));
        }
        public async Task<(long Length, string Mime)?> HeadAsync(string key, CancellationToken ct)
        {
            var head = await client.GetObjectMetadataAsync(Bucket, key, ct);
            return (head.ContentLength, head.Headers.ContentType);
        }
        public Task<MediaReadUrl> SignGetAsync(string key, CancellationToken ct)
        {
            var expires = DateTime.UtcNow.AddMinutes(4);
            return Task.FromResult(new MediaReadUrl(client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = Bucket,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = expires,
                Protocol = Protocol.HTTP
            }), expires));
        }
        public Task DeleteAsync(string key, CancellationToken ct) =>
            client.DeleteObjectAsync(Bucket, key, ct);
    }
}
